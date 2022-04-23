using Eventuous.Diagnostics;
using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using static Google.Cloud.PubSub.V1.PublisherClient;

// ReSharper disable InvertIf

namespace Eventuous.GooglePubSub.Producers;

/// <summary>
/// Producer for Google PubSub
/// </summary>
[PublicAPI]
public class GooglePubSubProducer : BaseProducer<PubSubProduceOptions>, IHostedService {
    readonly IEventSerializer _serializer;
    readonly ClientCache      _clientCache;
    readonly PubSubAttributes _attributes;

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="projectId">GCP project ID</param>
    /// <param name="serializer">Event serializer instance</param>
    /// <param name="settings"></param>
    /// <param name="clientCreationSettings"></param>
    public GooglePubSubProducer(
        string                  projectId,
        IEventSerializer?       serializer             = null,
        ClientCreationSettings? clientCreationSettings = null,
        Settings?               settings               = null
    ) : this(
        new PubSubProducerOptions {
            ProjectId              = Ensure.NotEmptyString(projectId),
            Settings               = settings,
            ClientCreationSettings = clientCreationSettings
        },
        serializer
    ) { }

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="options">Producer options</param>
    /// <param name="serializer">Optional: event serializer. Will use the default instance if missing.</param>
    public GooglePubSubProducer(
        PubSubProducerOptions options,
        IEventSerializer?     serializer = null
    ) : base(TracingOptions) {
        Ensure.NotNull(options);

        _serializer  = serializer ?? DefaultEventSerializer.Instance;
        _clientCache = new ClientCache(options);
        _attributes  = options.Attributes;
    }

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="options">Producer options</param>
    /// <param name="serializer">Optional: event serializer. Will use the default instance if missing.</param>
    public GooglePubSubProducer(
        IOptions<PubSubProducerOptions> options,
        IEventSerializer?               serializer = null
    ) : this(options.Value, serializer) { }

    public Task StartAsync(CancellationToken cancellationToken = default) {
        ReadyNow();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => Task.WhenAll(_clientCache.GetAllClients().Select(x => x.ShutdownAsync(cancellationToken)));

    static readonly ProducerTracingOptions TracingOptions = new() {
        MessagingSystem  = "google-pubsub",
        DestinationKind  = "topc",
        ProduceOperation = "publish"
    };

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        PubSubProduceOptions?        options,
        CancellationToken            cancellationToken = default
    ) {
        var client = await _clientCache.GetOrAddPublisher(stream, cancellationToken).NoContext();

        await Task.WhenAll(messages.Select(x => client.PublishAsync(CreateMessage(x, options)))).NoContext();
    }

    PubsubMessage CreateMessage(ProducedMessage message, PubSubProduceOptions? options) {
        var (eventType, contentType, payload) = _serializer.SerializeEvent(message.Message);

        var psm = new PubsubMessage {
            Data        = ByteString.CopyFrom(payload),
            OrderingKey = options?.OrderingKey ?? "",
            Attributes = {
                { _attributes.ContentType, contentType },
                { _attributes.EventType, eventType }
            },
            MessageId = message.MessageId.ToString()
        };

        if (message.Metadata != null) {
            message.Metadata.Remove(MetaTags.MessageId);

            foreach (var (key, value) in message.Metadata) {
                if (value != null)
                    psm.Attributes.Add(key, value.ToString());
            }
        }

        var attrs = options?.AddAttributes?.Invoke(message);

        if (attrs != null) {
            foreach (var (key, value) in attrs) {
                psm.Attributes.Add(key, value);
            }
        }

        return psm;
    }
}