using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using RabbitMQ.Client;

namespace Eventuous.Producers.RabbitMq {
    /// <summary>
    /// RabbitMQ producer
    /// </summary>
    [PublicAPI]
    public class RabbitMqProducer : BaseProducer<RabbitMqProduceOptions> {
        readonly RabbitMqExchangeOptions? _options;
        readonly IEventSerializer         _serializer;
        readonly string                   _exchange;
        readonly ConnectionFactory        _connectionFactory;

        IConnection? _connection;
        IModel?      _channel;

        /// <summary>
        /// Creates a RabbitMQ producer instance
        /// </summary>
        /// <param name="connectionFactory">RabbitMQ connection factory</param>
        /// <param name="exchange">Exchange name, will be created if doesn't exist</param>
        /// <param name="serializer">Event serializer instance</param>
        /// <param name="options">Additional configuration for the exchange</param>
        public RabbitMqProducer(
            ConnectionFactory        connectionFactory,
            string                   exchange,
            IEventSerializer         serializer,
            RabbitMqExchangeOptions? options = null
        ) {
            _options           = options;
            _serializer        = Ensure.NotNull(serializer, nameof(serializer));
            _exchange          = Ensure.NotEmptyString(exchange, nameof(exchange));
            _connectionFactory = Ensure.NotNull(connectionFactory, nameof(connectionFactory));
        }

        public override Task Initialize(CancellationToken cancellationToken = default) {
            _connection = _connectionFactory.CreateConnection();
            _channel    = _connection.CreateModel();
            _channel.ConfirmSelect();

            _channel.ExchangeDeclare(
                _exchange,
                _options?.Type ?? ExchangeType.Fanout,
                _options?.Durable ?? true,
                _options?.AutoDelete ?? false,
                _options?.Arguments
            );

            return Task.CompletedTask;
        }

        protected override async Task ProduceMany(
            IEnumerable<object>     messages,
            RabbitMqProduceOptions? options,
            CancellationToken       cancellationToken
        ) {
            foreach (var message in messages) {
                Publish(message, message.GetType(), options);
            }

            await Confirm(cancellationToken);
        }

        protected override Task ProduceOne(
            object                  message,
            Type                    type,
            RabbitMqProduceOptions? options,
            CancellationToken       cancellationToken
        ) {
            Publish(message, type, options);
            return Confirm(cancellationToken);
        }

        void Publish(object message, Type type, RabbitMqProduceOptions? options) {
            if (_channel == null)
                throw new InvalidOperationException("Producer hasn't been initialized, call Initialize");

            var payload   = _serializer.Serialize(message);
            var eventType = TypeMap.GetTypeNameByType(type);

            var prop = _channel.CreateBasicProperties();
            prop.ContentType  = _serializer.ContentType;
            prop.DeliveryMode = options?.DeliveryMode ?? RabbitMqProduceOptions.DefaultDeliveryMode;
            prop.Type         = eventType;

            if (options != null) {
                prop.Expiration    = options.Expiration;
                prop.Headers       = options.Headers;
                prop.Persistent    = options.Persisted;
                prop.Priority      = options.Priority;
                prop.AppId         = options.AppId;
                prop.CorrelationId = options.CorrelationId;
                prop.MessageId     = options.MessageId;
                prop.ReplyTo       = options.ReplyTo;
            }

            _channel.BasicPublish(_exchange, options?.RoutingKey ?? "", true, prop, payload);
        }

        async Task Confirm(CancellationToken cancellationToken) {
            while (!_channel!.WaitForConfirms(ConfirmTimeout) && !cancellationToken.IsCancellationRequested) {
                await Task.Delay(ConfirmIdle, cancellationToken);
            }
        }

        const string EventType = "event-type";

        static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(1);
        static readonly TimeSpan ConfirmIdle    = TimeSpan.FromMilliseconds(100);

        public override Task Shutdown(CancellationToken cancellationToken = default) {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();

            return Task.CompletedTask;
        }
    }
}