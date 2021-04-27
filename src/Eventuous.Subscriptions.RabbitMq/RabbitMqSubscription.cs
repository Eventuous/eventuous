using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eventuous.Subscriptions.NoOps;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Eventuous.Subscriptions.RabbitMq {
    /// <summary>
    /// RabbitMQ subscription service
    /// </summary>
    [PublicAPI]
    public class RabbitMqSubscriptionService : SubscriptionService {
        readonly IConnection _connection;
        readonly IModel      _channel;
        readonly string      _subscriptionQueue;
        readonly string      _exchange;
        readonly int         _concurrencyLimit;

        ILogger<RabbitMqSubscriptionService>? _log;

        /// <summary>
        /// Creates RabbitMQ subscription service instance
        /// </summary>
        /// <param name="connectionFactory">RabbitMQ connection factory</param>
        /// <param name="subscriptionQueue">Subscription queue, will be created it doesn't exist</param>
        /// <param name="exchange">Exchange to consume events from, the queue will get bound to this exchange</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="concurrencyLimit">The number of concurrent consumers</param>
        /// <param name="loggerFactory">Optional: logging factory</param>
        public RabbitMqSubscriptionService(
            ConnectionFactory          connectionFactory,
            string                     subscriptionQueue,
            string                     exchange,
            string                     subscriptionId,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            int                        concurrencyLimit = 1,
            ILoggerFactory?            loggerFactory    = null
        ) : base(subscriptionId, new NoOpCheckpointStore(), eventSerializer, eventHandlers, loggerFactory, new NoOpGapMeasure()) {
            _log = loggerFactory?.CreateLogger<RabbitMqSubscriptionService>();

            _connection = Ensure.NotNull(connectionFactory, nameof(connectionFactory)).CreateConnection();
            _channel    = _connection.CreateModel();
            var prefetch = _concurrencyLimit * 10;
            _channel.BasicQos((uint) prefetch, (ushort) prefetch, false);
            
            _subscriptionQueue = Ensure.NotEmptyString(subscriptionQueue, nameof(subscriptionQueue));
            _exchange          = Ensure.NotEmptyString(exchange, nameof(exchange));
            _concurrencyLimit  = concurrencyLimit;
        }

        protected override Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var cts = new CancellationTokenSource();

            _channel.ExchangeDeclare(_exchange, ExchangeType.Fanout, true);
            _channel.QueueDeclare(_subscriptionQueue, true, false, false, null);
            _channel.QueueBind(_subscriptionQueue, _exchange, _subscriptionQueue, null);

            var consumeChannel = Channel.CreateBounded<ReceivedEvent>(_concurrencyLimit * 10);

            for (var i = 0; i < _concurrencyLimit; i++) {
                Task.Run(RunConsumer, CancellationToken.None);
            }

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += (sender, args) => HandleReceived(sender, args, consumeChannel.Writer);

            _channel.BasicConsume(consumer, _subscriptionQueue);

            return Task.FromResult(new EventSubscription(SubscriptionId, new Stoppable(CloseConnection)));

            async Task RunConsumer() {
                while (!cts.IsCancellationRequested) {
                    ReceivedEvent re;

                    try {
                        re = await consumeChannel.Reader.ReadAsync(cts.Token);
                    }
                    catch (ChannelClosedException) {
                        return;
                    }
                    catch (Exception e) {
                        _log?.LogError(e, "Error while reading from the consume channel: {Message}", e.Message);
                        throw;
                    }

                    try {
                        await Handler(re, cts.Token);
                        _channel.BasicAck(re.Sequence, false);
                    }
                    catch (OperationCanceledException) {
                        _log?.LogInformation("Stopping RabbitMq subscription");
                        // expected
                    }
                    catch (Exception e) {
                        _log?.LogWarning(e, "Error in the consumer, will redeliver");
                        _channel.BasicReject(re.Sequence, true);
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
            object                       sender,
            BasicDeliverEventArgs        received,
            ChannelWriter<ReceivedEvent> writer
        ) {
            _log?.LogDebug("Consuming from RabbitMq");

            try {
                var receivedEvent = new ReceivedEvent {
                    Created     = received.BasicProperties.Timestamp.ToDateTime(),
                    Data        = received.Body.ToArray(),
                    EventId     = received.BasicProperties.MessageId,
                    EventType   = received.BasicProperties.Type,
                    ContentType = received.BasicProperties.ContentType,
                    Sequence    = received.DeliveryTag
                };

                await writer.WriteAsync(receivedEvent);
            }
            catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        protected override Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            // Needs the management API calls implementation
            // The queue size gives us the gap, but we can't measure the lead time
            return Task.FromResult(new EventPosition(0, DateTime.Now));
        }
    }
}