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
        readonly ConnectionFactory        _connectionFactory;

        IConnection? _connection;
        IModel?      _channel;

        /// <summary>
        /// Creates a RabbitMQ producer instance
        /// </summary>
        /// <param name="connectionFactory">RabbitMQ connection factory</param>
        /// <param name="serializer">Event serializer instance</param>
        /// <param name="options">Additional configuration for the exchange</param>
        public RabbitMqProducer(
            ConnectionFactory        connectionFactory,
            IEventSerializer?        serializer = null,
            RabbitMqExchangeOptions? options = null
        ) {
            _options           = options;
            _serializer        = serializer ?? DefaultEventSerializer.Instance;
            _connectionFactory = Ensure.NotNull(connectionFactory, nameof(connectionFactory));
        }

        public override Task Initialize(CancellationToken cancellationToken = default) {
            _connection = _connectionFactory.CreateConnection();
            _channel    = _connection.CreateModel();
            _channel.ConfirmSelect();

            return Task.CompletedTask;
        }

        protected override async Task ProduceMany(
            string                  stream,
            IEnumerable<object>     messages,
            RabbitMqProduceOptions? options,
            CancellationToken       cancellationToken
        ) {
            EnsureExchange(stream);
            
            foreach (var message in messages) {
                Publish(stream, message, message.GetType(), options);
            }

            await Confirm(cancellationToken).NoContext();
        }

        protected override Task ProduceOne(
            string                  stream,
            object                  message,
            Type                    type,
            RabbitMqProduceOptions? options,
            CancellationToken       cancellationToken
        ) {
            EnsureExchange(stream);
            Publish(stream, message, type, options);
            return Confirm(cancellationToken);
        }

        void Publish(string stream, object message, Type type, RabbitMqProduceOptions? options) {
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

            _channel.BasicPublish(stream, options?.RoutingKey ?? "", true, prop, payload);
        }

        readonly ExchangeCache _exchangeCache = new();

        void EnsureExchange(string exchange) {
            _exchangeCache.EnsureExchange(
                exchange,
                () =>
                    _channel!.ExchangeDeclare(
                        exchange,
                        _options?.Type ?? ExchangeType.Fanout,
                        _options?.Durable ?? true,
                        _options?.AutoDelete ?? false,
                        _options?.Arguments
                    )
            );
        }

        async Task Confirm(CancellationToken cancellationToken) {
            while (!_channel!.WaitForConfirms(ConfirmTimeout) && !cancellationToken.IsCancellationRequested) {
                await Task.Delay(ConfirmIdle, cancellationToken).NoContext();
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