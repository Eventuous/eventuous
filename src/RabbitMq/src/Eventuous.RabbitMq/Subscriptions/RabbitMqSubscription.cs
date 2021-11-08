using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eventuous.RabbitMq.Subscriptions;

/// <summary>
/// RabbitMQ subscription service
/// </summary>
[PublicAPI]
public class RabbitMqSubscription : EventSubscription<RabbitMqSubscriptionOptions> {
    public delegate void HandleEventProcessingFailure(
        IModel                channel,
        BasicDeliverEventArgs message,
        Exception             exception
    );

    readonly HandleEventProcessingFailure _failureHandler;
    readonly IConnection                  _connection;
    readonly IModel                       _channel;

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="options"></param>
    /// <param name="consumer"></param>
    /// <param name="loggerFactory">Optional: logging factory</param>
    public RabbitMqSubscription(
        ConnectionFactory                     connectionFactory,
        IOptions<RabbitMqSubscriptionOptions> options,
        MessageConsumer                       consumer,
        ILoggerFactory?                       loggerFactory = null
    ) : this(connectionFactory, options.Value, consumer, loggerFactory) { }

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="options"></param>
    /// <param name="consumer"></param>
    /// <param name="loggerFactory">Optional: logging factory</param>
    public RabbitMqSubscription(
        ConnectionFactory           connectionFactory,
        RabbitMqSubscriptionOptions options,
        MessageConsumer             consumer,
        ILoggerFactory?             loggerFactory = null
    )
        : base(
            Ensure.NotNull(options, nameof(options)),
            new ConcurrentConsumer(consumer, options.ConcurrencyLimit),
            loggerFactory
        ) {
        _failureHandler = options.FailureHandler ?? DefaultEventFailureHandler;

        _connection = Ensure.NotNull(connectionFactory, nameof(connectionFactory))
            .CreateConnection();

        _channel = _connection.CreateModel();

        var prefetch = Options.PrefetchCount > 0 ? Options.PrefetchCount
            : Options.ConcurrencyLimit * 2;

        _channel.BasicQos(0, (ushort)prefetch, false);
    }

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory</param>
    /// <param name="exchange">Exchange to consume events from, the queue will get bound to this exchange</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="consumer"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="loggerFactory">Optional: logging factory</param>
    public RabbitMqSubscription(
        ConnectionFactory connectionFactory,
        string            exchange,
        string            subscriptionId,
        MessageConsumer   consumer,
        IEventSerializer? eventSerializer = null,
        ILoggerFactory?   loggerFactory   = null
    ) : this(
        connectionFactory,
        new RabbitMqSubscriptionOptions {
            Exchange        = exchange,
            SubscriptionId  = subscriptionId,
            EventSerializer = eventSerializer
        },
        consumer,
        loggerFactory
    ) { }

    protected override ValueTask Subscribe(CancellationToken cancellationToken) {
        var exchange = Ensure.NotEmptyString(Options.Exchange, nameof(Options.Exchange));

        Log?.LogInformation("Ensuring exchange {Exchange}", exchange);

        _channel.ExchangeDeclare(
            exchange,
            Options.ExchangeOptions?.Type ?? ExchangeType.Fanout,
            Options.ExchangeOptions?.Durable ?? true,
            Options.ExchangeOptions?.AutoDelete ?? true,
            Options.ExchangeOptions?.Arguments
        );

        Log?.LogInformation("Ensuring queue {Queue}", Options.SubscriptionId);

        _channel.QueueDeclare(
            Options.SubscriptionId,
            Options.QueueOptions?.Durable ?? true,
            Options.QueueOptions?.Exclusive ?? false,
            Options.QueueOptions?.AutoDelete ?? false,
            Options.QueueOptions?.Arguments
        );

        Log?.LogInformation("Binding {Exchange} to {Queue}", exchange, Options.SubscriptionId);

        _channel.QueueBind(
            Options.SubscriptionId,
            exchange,
            Options.BindingOptions?.RoutingKey ?? "",
            Options.BindingOptions?.Arguments
        );

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleReceived;

        _channel.BasicConsume(consumer, Options.SubscriptionId);

        return default;
    }

    async Task HandleReceived(
        object                sender,
        BasicDeliverEventArgs received
    ) {
        DebugLog?.Invoke("Received message {MessageType}", received.BasicProperties.Type);

        try {
            var ctx = CreateContext(sender, received);
            await Handler(new DelayedAckConsumeContext(Ack, ctx)).NoContext();
        }
        catch (Exception e) {
            Log?.Log(
                Options.ThrowOnError ? LogLevel.Error : LogLevel.Warning,
                e,
                "Deserialization failed"
            );

            // This won't stop the subscription, but the reader will be gone. Not sure how to solve this one.
            if (Options.ThrowOnError) throw;
        }

        ValueTask Ack(CancellationToken _) {
            _channel.BasicAck(received.DeliveryTag, false);
            return default;
        }
    }

    IMessageConsumeContext CreateContext(object sender, BasicDeliverEventArgs received) {
        var evt = DeserializeData(
            received.BasicProperties.ContentType,
            received.BasicProperties.Type,
            received.Body,
            received.Exchange
        );

        var meta = received.BasicProperties.Headers != null
            ? new Metadata(received.BasicProperties.Headers.ToDictionary(x => x.Key, x => x.Value))
            : null;

        return new MessageConsumeContext(
            received.BasicProperties.MessageId,
            received.BasicProperties.Type,
            received.BasicProperties.ContentType,
            received.Exchange,
            received.DeliveryTag,
            received.BasicProperties.Timestamp.ToDateTime(),
            evt,
            meta,
            default
        );
    }

    protected override ValueTask Unsubscribe(CancellationToken cancellationToken) {
        _channel.Close();
        _channel.Dispose();
        _connection.Close();
        _connection.Dispose();

        // return _worker?.DisposeAsync() ?? default;
        return default;
    }

    void DefaultEventFailureHandler(
        IModel                channel,
        BasicDeliverEventArgs message,
        Exception             exception
    ) {
        Log?.LogWarning(exception, "Error in the consumer, will redeliver");
        _channel.BasicReject(message.DeliveryTag, true);
    }

    record Event(BasicDeliverEventArgs Original, IMessageConsumeContext Context);
}