using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eventuous.Subscriptions.RabbitMq;

/// <summary>
/// RabbitMQ subscription service
/// </summary>
[PublicAPI]
public class RabbitMqSubscriptionService : SubscriptionService {
    public delegate void HandleEventProcessingFailure(
        IModel                channel,
        BasicDeliverEventArgs message,
        Exception             exception
    );

    readonly RabbitMqSubscriptionOptions? _options;
    readonly HandleEventProcessingFailure _failureHandler;
    readonly IConnection                  _connection;
    readonly IModel                       _channel;
    readonly string                       _subscriptionQueue;
    readonly string                       _exchange;
    readonly int                          _concurrencyLimit;

    ILogger<RabbitMqSubscriptionService>? _log;

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="options"></param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="loggerFactory">Optional: logging factory</param>
    public RabbitMqSubscriptionService(
        ConnectionFactory                     connectionFactory,
        IOptions<RabbitMqSubscriptionOptions> options,
        IEnumerable<IEventHandler>            eventHandlers,
        IEventSerializer?                     eventSerializer = null,
        ILoggerFactory?                       loggerFactory   = null
    ) : this(connectionFactory, options.Value, eventHandlers, eventSerializer, loggerFactory) { }

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="options"></param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="loggerFactory">Optional: logging factory</param>
    public RabbitMqSubscriptionService(
        ConnectionFactory           connectionFactory,
        RabbitMqSubscriptionOptions options,
        IEnumerable<IEventHandler>  eventHandlers,
        IEventSerializer?           eventSerializer = null,
        ILoggerFactory?             loggerFactory   = null
    )
        : base(
            Ensure.NotNull(options, nameof(options)),
            new NoOpCheckpointStore(),
            eventHandlers,
            eventSerializer,
            loggerFactory,
            new NoOpGapMeasure()
        ) {
        _options = options;

        _log              = loggerFactory?.CreateLogger<RabbitMqSubscriptionService>();
        _failureHandler   = options.FailureHandler ?? DefaultEventFailureHandler;
        _concurrencyLimit = options.ConcurrencyLimit;

        _connection = Ensure.NotNull(connectionFactory, nameof(connectionFactory)).CreateConnection();

        _channel = _connection.CreateModel();

        var prefetch = _concurrencyLimit * 10;
        _channel.BasicQos(0, (ushort)prefetch, false);

        _subscriptionQueue = Ensure.NotEmptyString(options.SubscriptionQueue, nameof(options.SubscriptionQueue));
        _exchange          = Ensure.NotEmptyString(options.Exchange, nameof(options.Exchange));
    }

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory</param>
    /// <param name="subscriptionQueue">Subscription queue, will be created it doesn't exist</param>
    /// <param name="exchange">Exchange to consume events from, the queue will get bound to this exchange</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="loggerFactory">Optional: logging factory</param>
    public RabbitMqSubscriptionService(
        ConnectionFactory          connectionFactory,
        string                     subscriptionQueue,
        string                     exchange,
        string                     subscriptionId,
        IEnumerable<IEventHandler> eventHandlers,
        IEventSerializer?          eventSerializer = null,
        ILoggerFactory?            loggerFactory   = null
    ) : this(
        connectionFactory,
        new RabbitMqSubscriptionOptions {
            SubscriptionQueue = subscriptionQueue,
            Exchange          = exchange,
            SubscriptionId    = subscriptionId
        },
        eventHandlers,
        eventSerializer,
        loggerFactory
    ) { }

    protected override Task<EventSubscription> Subscribe(
        Checkpoint        checkpoint,
        CancellationToken cancellationToken
    ) {
        var cts = new CancellationTokenSource();

        Log.LogDebug("Ensuring exchange {Exchange}", _exchange);

        _channel.ExchangeDeclare(
            _exchange,
            _options?.ExchangeOptions?.Type ?? ExchangeType.Fanout,
            _options?.ExchangeOptions?.Durable ?? true,
            _options?.ExchangeOptions?.AutoDelete ?? true,
            _options?.ExchangeOptions?.Arguments
        );

        Log.LogDebug("Ensuring queue {Queue}", _subscriptionQueue);

        _channel.QueueDeclare(
            _subscriptionQueue,
            _options?.QueueOptions?.Durable ?? true,
            _options?.QueueOptions?.Exclusive ?? false,
            _options?.QueueOptions?.AutoDelete ?? false,
            _options?.QueueOptions?.Arguments
        );

        Log.LogDebug("Binding {Exchange} to {Queue}", _exchange, _subscriptionQueue);

        _channel.QueueBind(
            _subscriptionQueue,
            _exchange,
            _options?.BindingOptions?.RoutingKey ?? "",
            _options?.BindingOptions?.Arguments
        );

        var consumeChannel = Channel.CreateBounded<Event>(_concurrencyLimit * 10);

        for (var i = 0; i < _concurrencyLimit; i++) {
            Task.Run(RunConsumer, CancellationToken.None);
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += (sender, args) => HandleReceived(sender, args, consumeChannel.Writer);

        _channel.BasicConsume(consumer, _subscriptionQueue);

        return Task.FromResult(new EventSubscription(SubscriptionId, new Stoppable(CloseConnection)));

        async Task RunConsumer() {
            _log.LogDebug("Started consumer instance for {Queue}", _subscriptionQueue);

            while (!consumeChannel.Reader.Completion.IsCompleted) {
                var evt = await TryGetMessage();
                if (evt == null) continue;

                try {
                    _log.LogDebug("Handling message {MessageId} from {Queue}", evt.ReceivedEvent.EventId, _subscriptionQueue);
                    await Handler(evt.ReceivedEvent, CancellationToken.None).NoContext();
                    _channel.BasicAck(evt.Original.DeliveryTag, false);
                }
                catch (Exception e) {
                    _failureHandler(_channel, evt.Original, e);
                }
            }

            _log.LogDebug("Stopped consumer instance for {Queue}", _subscriptionQueue);
        }

        async Task<Event?> TryGetMessage() {
            try {
                return await consumeChannel.Reader.ReadAsync(cts.Token);
            }
            catch (OperationCanceledException) {
                // Expected
                return null;
            }
            catch (Exception e) {
                Log.LogError(e, "Unable to read message from the channel: {Message}", e.Message);
                throw;
            }
        }

        void CloseConnection() {
            _channel.Close();
            _channel.Dispose();
            _connection.Close();
            _connection.Dispose();
            
            cts.Cancel();
            consumeChannel.Writer.Complete();
            consumeChannel.Reader.Completion.GetAwaiter().GetResult();
            cts.Dispose();
        }
    }

    async Task HandleReceived(
        object                sender,
        BasicDeliverEventArgs received,
        ChannelWriter<Event>  writer
    ) {
        Log.LogTrace("Received message {MessageType}", received.BasicProperties.Type);

        try {
            var receivedEvent = TryHandleReceived(sender, received);
            await writer.WriteAsync(new Event(received, receivedEvent)).NoContext();
        }
        catch (Exception e) {
            Log.Log(FailOnError ? LogLevel.Error : LogLevel.Warning, e, "Deserialization failed");

            // This won't stop the subscription, but the reader will be gone. Not sure how to solve this one.
            if (FailOnError) throw;
        }
    }

    ReceivedEvent TryHandleReceived(object sender, BasicDeliverEventArgs received) {
        var evt = DeserializeData(
            received.BasicProperties.ContentType,
            received.BasicProperties.Type,
            received.Body,
            received.Exchange
        );

        var meta = received.BasicProperties.Headers != null
            ? new Metadata(received.BasicProperties.Headers.ToDictionary(x => x.Key, x => x.Value))
            : null;

        return new ReceivedEvent(
            received.BasicProperties.MessageId,
            received.BasicProperties.Type,
            received.BasicProperties.ContentType,
            0,
            0,
            received.Exchange,
            received.DeliveryTag,
            received.BasicProperties.Timestamp.ToDateTime(),
            evt,
            meta
        );
    }

    protected override Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
        // Needs the management API calls implementation
        // The queue size gives us the gap, but we can't measure the lead time
        return Task.FromResult(new EventPosition(0, DateTime.Now));
    }

    void DefaultEventFailureHandler(IModel channel, BasicDeliverEventArgs message, Exception exception) {
        _log?.LogWarning(exception, "Error in the consumer, will redeliver");
        _channel.BasicReject(message.DeliveryTag, true);
    }

    record Event(BasicDeliverEventArgs Original, ReceivedEvent ReceivedEvent);
}