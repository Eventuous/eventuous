using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Eventuous.Tools;

namespace Eventuous.EventStore.Producers;

/// <summary>
/// Producer for EventStoreDB
/// </summary>
[PublicAPI]
public class EventStoreProducer : BaseProducer<EventStoreProduceOptions> {
    readonly EventStoreClient    _client;
    readonly IEventSerializer    _serializer;
    readonly IMetadataSerializer _metaSerializer;

    /// <summary>
    /// Create a new EventStoreDB producer instance
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client</param>
    /// <param name="serializer">Optional: event serializer instance</param>
    /// <param name="metaSerializer">Optional: metadata serializer instance</param>
    public EventStoreProducer(
        EventStoreClient     eventStoreClient,
        IEventSerializer?    serializer     = null,
        IMetadataSerializer? metaSerializer = null
    ) : base(TracingOptions) {
        _client         = Ensure.NotNull(eventStoreClient);
        _serializer     = serializer     ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
    }

    /// <summary>
    /// Create a new EventStoreDB producer instance
    /// </summary>
    /// <param name="clientSettings">EventStoreDB gRPC client settings</param>
    /// <param name="serializer">Optional: event serializer instance</param>
    /// <param name="metaSerializer">Optional: metadata serializer instance</param>
    public EventStoreProducer(
        EventStoreClientSettings clientSettings,
        IEventSerializer?        serializer     = null,
        IMetadataSerializer?     metaSerializer = null
    ) : this(new EventStoreClient(Ensure.NotNull(clientSettings)), serializer, metaSerializer) { }

    static readonly ProducerTracingOptions TracingOptions = new() {
        DestinationKind  = "stream",
        MessagingSystem  = "eventstoredb",
        ProduceOperation = "append"
    };

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        EventStoreProduceOptions?    produceOptions,
        CancellationToken            cancellationToken = default
    ) {
        var options = produceOptions ?? EventStoreProduceOptions.Default;

        foreach (var chunk in Ensure.NotNull(messages).Chunks(options.MaxAppendEventsCount)) {
            var chunkMessages = chunk.ToArray();

            try {
                await _client.AppendToStreamAsync(
                        stream,
                        options.ExpectedState,
                        chunkMessages.Select(message => CreateMessage(message)),
                        null,
                        options.Deadline,
                        options.Credentials,
                        cancellationToken
                    )
                    .NoContext();

                await chunkMessages.Select(x => x.Ack<EventStoreProducer>()).WhenAll().NoContext();
            }
            catch (Exception e) {
                await chunkMessages
                    .Select(x => x.Nack<EventStoreProducer>("Unable to produce to EventStoreDB", e))
                    .WhenAll()
                    .NoContext();
            }
        }
    }

    EventData CreateMessage(ProducedMessage message) {
        var msg = Ensure.NotNull(message.Message);
        var (eventType, contentType, payload) = _serializer.SerializeEvent(msg);
        message.Metadata!.Remove(MetaTags.MessageId);
        var metaBytes = _metaSerializer.Serialize(message.Metadata);

        return new EventData(Uuid.FromGuid(message.MessageId), eventType, payload, metaBytes, contentType);
    }
}
