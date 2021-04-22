using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Eventuous.Subscriptions.RabbitMq {
    [PublicAPI]
    public class RabbitMqSubscriptionService : SubscriptionService {
        readonly IConnection       _connection;
        readonly IModel            _channel;
        readonly string            _subscriptionQueue;
        readonly string            _exchange;
        AsyncEventingBasicConsumer _consumer = null!;

        public RabbitMqSubscriptionService(
            ConnectionFactory          connectionFactory,
            string                     subscriptionQueue,
            string                     exchange,
            string                     subscriptionId,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) : base(subscriptionId, new NoOpCheckpointStore(), eventSerializer, eventHandlers, loggerFactory, measure) {
            _connection        = connectionFactory.CreateConnection();
            _channel           = _connection.CreateModel();
            _subscriptionQueue = subscriptionQueue;
            _exchange          = exchange;
        }

        protected override Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            _channel.ExchangeDeclare(_exchange, ExchangeType.Fanout, true);
            _channel.QueueDeclare(_subscriptionQueue, true, false, false, null);
            _channel.QueueBind(_subscriptionQueue, _exchange, _subscriptionQueue, null);

            _consumer          =  new AsyncEventingBasicConsumer(_channel);
            _consumer.Received += HandleReceived;

            _channel.BasicConsume(_consumer, _subscriptionQueue);

            return Task.FromResult(new EventSubscription(SubscriptionId, new Disposable(CloseConnection)));
        }

        async Task HandleReceived(object sender, BasicDeliverEventArgs received) {
            try {
                var receivedEvent = new ReceivedEvent {
                    Created     = received.BasicProperties.Timestamp.ToDateTime(),
                    Data        = received.Body,
                    EventId     = received.BasicProperties.MessageId,
                    EventType   = received.BasicProperties.Type,
                    ContentType = received.BasicProperties.ContentType
                };

                await Handler(receivedEvent, CancellationToken.None);
                _channel.BasicAck(received.DeliveryTag, false);
            }
            catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        void CloseConnection() {
            _channel.Close();
            _channel.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        protected override Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            // Needs the management API calls implementation
            // The queue size gives us the gap, but we can't measure the lead time
            return Task.FromResult(new EventPosition(0, DateTime.Now));
        }
    }
}