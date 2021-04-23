using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using RabbitMQ.Client;

namespace Eventuous.Producers.RabbitMq {
    [PublicAPI]
    public class RabbitMqProducer : BaseProducer, IDisposable {
        readonly IEventSerializer _serializer;
        readonly IConnection      _connection;
        readonly IModel           _channel;
        readonly string           _exchange;

        public RabbitMqProducer(ConnectionFactory connectionFactory, string exchange, IEventSerializer serializer) {
            _serializer = Ensure.NotNull(serializer, nameof(serializer));
            _exchange   = Ensure.NotEmptyString(exchange, nameof(exchange));

            // this name will be shared by all connections instantiated by this factory
            // factory.ClientProvidedName = "app:audit component:event-consumer"
            _connection = Ensure.NotNull(connectionFactory, nameof(connectionFactory)).CreateConnection();
            _channel    = _connection.CreateModel();
            _channel.ConfirmSelect();

            // Make it configurable
            _channel.ExchangeDeclare(exchange, ExchangeType.Fanout, true);
        }

        protected override async Task ProduceMany(IEnumerable<object> messages, CancellationToken cancellationToken) {
            foreach (var message in messages) {
                Publish(message, message.GetType());
            }

            await Confirm(cancellationToken);
        }

        protected override Task ProduceOne(object message, Type type, CancellationToken cancellationToken) {
            Publish(message, type);
            return Confirm(cancellationToken);
        }

        void Publish(object message, Type type) {
            var payload   = _serializer.Serialize(message);
            var eventType = TypeMap.GetTypeNameByType(type);
            var prop      = _channel.CreateBasicProperties();
            prop.ContentType  = _serializer.ContentType;
            prop.DeliveryMode = 2;
            prop.Type         = eventType;
            
            _channel.BasicPublish(_exchange, eventType, true, prop, payload);
        }

        async Task Confirm(CancellationToken cancellationToken) {
            while (!_channel.WaitForConfirms(ConfirmTimeout) && !cancellationToken.IsCancellationRequested) {
                await Task.Delay(ConfirmIdle, cancellationToken);
            }
        }

        const string EventType = "event-type";

        static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(1);
        static readonly TimeSpan ConfirmIdle    = TimeSpan.FromMilliseconds(100);

        public void Dispose() {
            _channel.Close();
            _channel.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}