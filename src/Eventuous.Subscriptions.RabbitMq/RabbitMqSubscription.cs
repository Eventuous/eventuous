using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Eventuous.Subscriptions.RabbitMq {
    [PublicAPI]
    public class RabbitMqSubscriptionService : SubscriptionService {
        readonly IConnection _connection;
        readonly IModel      _channel;
        readonly string      _subscriptionQueue;
        readonly string      _exchange;
        readonly int         _concurrencyLimit;

        ILogger<RabbitMqSubscriptionService>? _log;

        public RabbitMqSubscriptionService(
            ConnectionFactory          connectionFactory,
            string                     subscriptionQueue,
            string                     exchange,
            string                     subscriptionId,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            int                        concurrencyLimit = 1,
            ILoggerFactory?            loggerFactory    = null,
            SubscriptionGapMeasure?    measure          = null
        ) : base(subscriptionId, new NoOpCheckpointStore(), eventSerializer, eventHandlers, loggerFactory, measure) {
            _log = loggerFactory?.CreateLogger<RabbitMqSubscriptionService>();

            _connection = connectionFactory.CreateConnection();
            _channel    = _connection.CreateModel();
            var prefetch = _concurrencyLimit * 10;
            _channel.BasicQos((uint) prefetch, (ushort) prefetch, false);
            _subscriptionQueue = subscriptionQueue;
            _exchange          = exchange;
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

            return Task.FromResult(new EventSubscription(SubscriptionId, new Disposable(CloseConnection)));

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