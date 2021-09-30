using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Google.Cloud.PubSub.V1.PublisherClient;

// ReSharper disable InvertIf

namespace Eventuous.Producers.GooglePubSub; 

/// <summary>
/// Producer for Google PubSub
/// </summary>
[PublicAPI]
public class GooglePubSubProducer : BaseProducer<PubSubProduceOptions>, IHostedService {
    readonly IEventSerializer _serializer;
    readonly ClientCache      _clientCache;
    readonly ILogger?         _log;
    readonly PubSubAttributes _attributes;

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="projectId">GCP project ID</param>
    /// <param name="serializer">Event serializer instance</param>
    /// <param name="settings"></param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="clientCreationSettings"></param>
    public GooglePubSubProducer(
        string                  projectId,
        IEventSerializer?       serializer             = null,
        ClientCreationSettings? clientCreationSettings = null,
        Settings?               settings               = null,
        ILoggerFactory?         loggerFactory          = null
    ) : this(
        new PubSubProducerOptions {
            ProjectId              = Ensure.NotEmptyString(projectId, nameof(projectId)),
            Settings               = settings,
            ClientCreationSettings = clientCreationSettings
        },
        serializer,
        loggerFactory
    ) { }

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="options">Producer options</param>
    /// <param name="serializer">Optional: event serializer. Will use the default instance if missing.</param>
    /// <param name="loggerFactory">Logger factory</param>
    public GooglePubSubProducer(
        PubSubProducerOptions options,
        IEventSerializer?     serializer    = null,
        ILoggerFactory?       loggerFactory = null
    ) {
        Ensure.NotNull(options, nameof(options));

        _serializer  = serializer ?? DefaultEventSerializer.Instance;
        _log         = loggerFactory?.CreateLogger($"Producer:{options.ProjectId}");
        _clientCache = new ClientCache(options, _log);
        _attributes  = options.Attributes;
    }

    public GooglePubSubProducer(
        IOptions<PubSubProducerOptions> options,
        IEventSerializer?               serializer    = null,
        TypeMapper?                     typeMapper    = null,
        ILoggerFactory?                 loggerFactory = null
    ) : this(options.Value, serializer, loggerFactory) { }

    public Task StartAsync(CancellationToken cancellationToken = default) {
        ReadyNow();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => Task.WhenAll(_clientCache.GetAllClients().Select(x => x.ShutdownAsync(cancellationToken)));

    public override async Task ProduceMessage(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        PubSubProduceOptions?        options,
        CancellationToken            cancellationToken = default
    ) {
        var client = await _clientCache.GetOrAddPublisher(stream, cancellationToken).NoContext();
        await Task.WhenAll(messages.Select(x => Produce(client, x, x.GetType(), options)));
    }

    async Task Produce(
        PublisherClient       client,
        object                message,
        Type                  type,
        PubSubProduceOptions? options
    ) {
        var pubSubMessage = CreateMessage(message, type, options);
        await client.PublishAsync(pubSubMessage).NoContext();
    }

    PubsubMessage CreateMessage(object message, Type type, PubSubProduceOptions? options) {
        var (eventType, payload) = _serializer.SerializeEvent(message);

        var psm = new PubsubMessage {
            Data        = ByteString.CopyFrom(payload),
            OrderingKey = options?.OrderingKey ?? "",
            Attributes = {
                { _attributes.ContentType, _serializer.ContentType },
                { _attributes.EventType, eventType }
            }
        };

        var attrs = options?.AddAttributes?.Invoke(message);

        if (attrs != null) {
            foreach (var (key, value) in attrs) {
                psm.Attributes.Add(key, value);
            }
        }

        return psm;
    }
}