using System;
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

            // Make it configurable
            _channel.ExchangeDeclare(exchange, ExchangeType.Fanout, true);
        }

        protected override Task Produce(object message, Type type) {
            var payload   = _serializer.Serialize(message);
            var eventType = TypeMap.GetTypeNameByType(type);

            var prop = _channel.CreateBasicProperties();
            prop.ContentType  = _serializer.ContentType;
            prop.DeliveryMode = 2;
            prop.Type         = eventType;

            _channel.BasicPublish(_exchange, eventType, true, prop, payload);
            return Task.CompletedTask;
        }

        const string EventType = "event-type";

        public void Dispose() {
            _channel.Close();
            _channel.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}