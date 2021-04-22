using System;
using RabbitMQ.Client;

namespace Eventuous.Tests.RabbitMq {
    public static class RabbitMqFixture {
        static RabbitMqFixture() {
            const string connectionString = "amqp://guest:guest@localhost:5672/";

            ConnectionFactory = new ConnectionFactory {
                Uri = new Uri(connectionString),
                DispatchConsumersAsync = true
            };
        }
        
        public static ConnectionFactory ConnectionFactory { get; }
    }
}