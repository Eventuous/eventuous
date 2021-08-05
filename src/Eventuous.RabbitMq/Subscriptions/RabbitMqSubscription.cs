using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.NoOps;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Eventuous.RabbitMq.Subscriptions {
    /// <summary>
    /// RabbitMQ subscription service
    /// </summary>
    [PublicAPI]
    public class RabbitMqSubscriptionService : SubscriptionService {
        public delegate void HandleEventProcessingFailure(
            IModel                channel,
            BasicDeliverEventArgs message,
            Exception             exception
        );

        readonly RabbitMqSubscriptionOptions? _options;
        readonly HandleEventProcessingFailure _failureHandler;
        readonly IConnection                  _connection;
        readonly IModel                       _channel;
        readonly string                       _subscriptionQueue;
        readonly string                       _exchange;
        readonly int                          _concurrencyLimit;

        ILogger<RabbitMqSubscriptionService>? _log;

        /// <summary>
        /// Creates RabbitMQ subscription service instance
        /// </summary>
        /// <param name="connectionFactory"></param>
        /// <param name="options"></param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="loggerFactory">Optional: logging factory</param>
        public RabbitMqSubscriptionService(
            ConnectionFactory                     connectionFactory,
            IOptions<RabbitMqSubscriptionOptions> options,
            IEnumerable<IEventHandler>            eventHandlers,
            IEventSerializer?                     eventSerializer = null,
            ILoggerFactory?                       loggerFactory   = null
        ) : this(connectionFactory, options.Value, eventHandlers, eventSerializer, loggerFactory) { }

        /// <summary>
        /// Creates RabbitMQ subscription service instance
        /// </summary>
        /// <param name="connectionFactory"></param>
        /// <param name="options"></param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="loggerFactory">Optional: logging factory</param>
        public RabbitMqSubscriptionService(
            ConnectionFactory           connectionFactory,
            RabbitMqSubscriptionOptions options,
            IEnumerable<IEventHandler>  eventHandlers,
            IEventSerializer?           eventSerializer = null,
            ILoggerFactory?             loggerFactory   = null
        )
            : base(
                Ensure.NotNull(options, nameof(options)),
                new NoOpCheckpointStore(),
                eventHandlers,
                eventSerializer,
                loggerFactory,
                new NoOpGapMeasure()
            ) {
            _options = options;

            _failureHandler   = options.FailureHandler ?? DefaultEventFailureHandler;
            _log              = loggerFactory?.CreateLogger<RabbitMqSubscriptionService>();
            _concurrencyLimit = options.ConcurrencyLimit;

            _connection = Ensure.NotNull(connectionFactory, nameof(connectionFactory)).CreateConnection();

            _channel = _connection.CreateModel();

            var prefetch = _concurrencyLimit * 10;
            _channel.BasicQos(0, (ushort) prefetch, false);

            _subscriptionQueue = Ensure.NotEmptyString(options.SubscriptionQueue, nameof(options.SubscriptionQueue));
            _exchange          = Ensure.NotEmptyString(options.Exchange, nameof(options.Exchange));
        }

        /// <summary>
        /// Creates RabbitMQ subscription service instance
        /// </summary>
        /// <param name="connectionFactory">RabbitMQ connection factory</param>
        /// <param name="subscriptionQueue">Subscription queue, will be created it doesn't exist</param>
        /// <param name="exchange">Exchange to consume events from, the queue will get bound to this exchange</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logging factory</param>
        public RabbitMqSubscriptionService(
            ConnectionFactory          connectionFactory,
            string                     subscriptionQueue,
            string                     exchange,
            string                     subscriptionId,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            ILoggerFactory?            loggerFactory   = null
        ) : this(
            connectionFactory,
            new RabbitMqSubscriptionOptions {
                SubscriptionQueue = subscriptionQueue,
                Exchange          = exchange,
                SubscriptionId    = subscriptionId
            },
            eventHandlers,
            eventSerializer,
            loggerFactory
        ) { }

        protected override Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var cts = new CancellationTokenSource();

            _channel.ExchangeDeclare(
                _exchange,
                _options?.ExchangeOptions?.Type ?? ExchangeType.Fanout,
                _options?.ExchangeOptions?.Durable ?? true,
                _options?.ExchangeOptions?.AutoDelete ?? true,
                _options?.ExchangeOptions?.Arguments
            );

            _channel.QueueDeclare(
                _subscriptionQueue,
                _options?.QueueOptions?.Durable ?? true,
                _options?.QueueOptions?.Exclusive ?? false,
                _options?.QueueOptions?.AutoDelete ?? false,
                _options?.QueueOptions?.Arguments
            );

            _channel.QueueBind(
                _subscriptionQueue,
                _exchange,
                _options?.BindingOptions?.RoutingKey ?? "",
                _options?.BindingOptions?.Arguments
            );

            var consumeChannel = Channel.CreateBounded<Event>(_concurrencyLimit * 10);

            for (var i = 0; i < _concurrencyLimit; i++) {
                Task.Run(RunConsumer, CancellationToken.None);
            }

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += (sender, args) => HandleReceived(sender, args, consumeChannel.Writer);

            _channel.BasicConsume(consumer, _subscriptionQueue);

            return Task.FromResult(new EventSubscription(SubscriptionId, new Stoppable(CloseConnection)));

            async Task RunConsumer() {
                while (!consumeChannel.Reader.Completion.IsCompleted) {
                    var (message, re) = await consumeChannel.Reader.ReadAsync(CancellationToken.None);

                    try {
                        await Handler(re, CancellationToken.None).NoContext();
                        _channel.BasicAck(message.DeliveryTag, false);
                    }
                    catch (Exception e) {
                        _failureHandler(_channel, message, e);
                    }
                }
            }

            void CloseConnection() {
                _channel.Close();
                _channel.Dispose();
                _connection.Close();
                _connection.Dispose();

                consumeChannel.Writer.Complete();
                cts.Cancel();
                cts.Dispose();
            }
        }

        async Task HandleReceived(
            object                sender,
            BasicDeliverEventArgs received,
            ChannelWriter<Event>  writer
        ) {
            var evt = DeserializeData(
                received.BasicProperties.ContentType,
                received.BasicProperties.Type,
                received.Body,
                received.Exchange
            );

            var receivedEvent = new ReceivedEvent(
                received.BasicProperties.MessageId,
                received.BasicProperties.Type,
                received.BasicProperties.ContentType,
                0,
                0,
                received.Exchange,
                received.DeliveryTag,
                received.BasicProperties.Timestamp.ToDateTime(),
                evt
            );

            await writer.WriteAsync(new Event(received, receivedEvent)).NoContext();
        }

        protected override Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            // Needs the management API calls implementation
            // The queue size gives us the gap, but we can't measure the lead time
            return Task.FromResult(new EventPosition(0, DateTime.Now));
        }

        void DefaultEventFailureHandler(IModel channel, BasicDeliverEventArgs message, Exception exception) {
            _log?.LogWarning(exception, "Error in the consumer, will redeliver");
            _channel.BasicReject(message.DeliveryTag, true);
        }

        record Event(BasicDeliverEventArgs Original, ReceivedEvent ReceivedEvent);
    }
}