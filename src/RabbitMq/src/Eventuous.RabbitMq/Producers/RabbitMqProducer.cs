using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Eventuous.RabbitMq.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Eventuous.RabbitMq.Producers;

/// <summary>
/// RabbitMQ producer
/// </summary>
[PublicAPI]
public class RabbitMqProducer : BaseProducer<RabbitMqProduceOptions>, IHostedService {
    readonly RabbitMqExchangeOptions? _options;
    readonly IEventSerializer         _serializer;
    readonly ConnectionFactory        _connectionFactory;

    IConnection? _connection;
    IModel?      _channel;

    /// <summary>
    /// Creates a RabbitMQ producer instance
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory</param>
    /// <param name="serializer">Optional: event serializer instance</param>
    /// <param name="options">Optional: additional configuration for the exchange</param>
    public RabbitMqProducer(
        ConnectionFactory        connectionFactory,
        IEventSerializer?        serializer = null,
        RabbitMqExchangeOptions? options    = null
    ) : base(TracingOptions) {
        _options           = options;
        _serializer        = serializer ?? DefaultEventSerializer.Instance;
        _connectionFactory = Ensure.NotNull(connectionFactory);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) {
        _connection = _connectionFactory.CreateConnection();
        _channel    = _connection.CreateModel();
        _channel.ConfirmSelect();

        ReadyNow();

        return Task.CompletedTask;
    }

    static readonly ProducerTracingOptions TracingOptions = new() {
        MessagingSystem  = "rabbitmq",
        DestinationKind  = "exchange",
        ProduceOperation = "publish"
    };

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        RabbitMqProduceOptions?      options,
        CancellationToken            cancellationToken = default
    ) {
        EnsureExchange(stream);

        foreach (var message in messages) {
            if (Activity.Current is { IsAllDataRequested: true }) {
                Activity.Current.SetTag(RabbitMqTelemetryTags.RoutingKey, options?.RoutingKey);
            }

            Publish(stream, message, options);
        }

        await Confirm(cancellationToken).NoContext();
    }

    void Publish(string stream, ProducedMessage message, RabbitMqProduceOptions? options) {
        if (_channel == null) throw new InvalidOperationException("Producer hasn't been initialized, call Initialize");

        var (msg, metadata)                   = (message.Message, message.Metadata);
        var (eventType, contentType, payload) = _serializer.SerializeEvent(msg);

        var prop = _channel.CreateBasicProperties();
        prop.ContentType   = contentType;
        prop.DeliveryMode  = options?.DeliveryMode ?? RabbitMqProduceOptions.DefaultDeliveryMode;
        prop.Type          = eventType;
        prop.CorrelationId = metadata!.GetCorrelationId();
        prop.MessageId     = message.MessageId.ToString();

        metadata!.Remove(MetaTags.MessageId);
        prop.Headers = metadata.ToDictionary(x => x.Key, x => x.Value);

        if (options != null) {
            prop.Expiration = options.Expiration;
            prop.Persistent = options.Persisted;
            prop.Priority   = options.Priority;
            prop.AppId      = options.AppId;
            prop.ReplyTo    = options.ReplyTo;
        }

        _channel.BasicPublish(stream, options?.RoutingKey ?? "", true, prop, payload);
    }

    readonly ExchangeCache _exchangeCache = new();

    void EnsureExchange(string exchange)
        => _exchangeCache.EnsureExchange(
            exchange,
            () =>
                _channel!.ExchangeDeclare(
                    exchange,
                    _options?.Type       ?? ExchangeType.Fanout,
                    _options?.Durable    ?? true,
                    _options?.AutoDelete ?? false,
                    _options?.Arguments
                )
        );

    async Task Confirm(CancellationToken cancellationToken) {
        while (!_channel!.WaitForConfirms(ConfirmTimeout) && !cancellationToken.IsCancellationRequested) {
            await Task.Delay(ConfirmIdle, cancellationToken).NoContext();
        }
    }

    const string EventType = "event-type";

    static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(1);
    static readonly TimeSpan ConfirmIdle    = TimeSpan.FromMilliseconds(100);

    public Task StopAsync(CancellationToken cancellationToken = default) {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();

        return Task.CompletedTask;
    }
}
