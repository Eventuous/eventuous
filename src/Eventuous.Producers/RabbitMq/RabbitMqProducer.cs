using System;
using RabbitMQ.Client;

namespace Eventuous.Producers.RabbitMq {
    public class RabbitMqProducer : IDisposable {
        readonly IConnection _connection;
        readonly IModel      _channel;

        public RabbitMqProducer(string connectionString) {
            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            // this name will be shared by all connections instantiated by this factory
            // factory.ClientProvidedName = "app:audit component:event-consumer"
            _connection                = factory.CreateConnection();
            _channel                   = _connection.CreateModel();
        }

        public void Dispose() {
            _channel.Close();
            _channel.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}