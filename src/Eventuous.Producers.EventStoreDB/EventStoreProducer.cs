using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Eventuous.Producers.EventStoreDB; 

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
    /// <param name="metaSerializer"></param>
    public EventStoreProducer(
        EventStoreClient     eventStoreClient,
        IEventSerializer?    serializer     = null,
        IMetadataSerializer? metaSerializer = null
    ) {
        _client         = Ensure.NotNull(eventStoreClient, nameof(eventStoreClient));
        _serializer     = serializer ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
            
        ReadyNow();
    }

    /// <summary>
    /// Create a new EventStoreDB producer instance
    /// </summary>
    /// <param name="clientSettings">EventStoreDB gRPC client settings</param>
    /// <param name="serializer">Optional: event serializer instance</param>
    /// <param name="metaSerializer"></param>
    public EventStoreProducer(
        EventStoreClientSettings clientSettings,
        IEventSerializer?        serializer     = null,
        IMetadataSerializer?     metaSerializer = null
    )
        : this(
            new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings))),
            serializer,
            metaSerializer
        ) { }

    public override async Task ProduceMessage(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        EventStoreProduceOptions?    produceOptions,
        CancellationToken            cancellationToken = default
    ) {
        var options = produceOptions ?? EventStoreProduceOptions.Default;
        var data = Ensure.NotNull(messages, nameof(messages))
            .Select(x => CreateMessage(x.Message, x.Message.GetType(), x.Metadata));

        foreach (var chunk in data.Chunks(options.MaxAppendEventsCound)) {
            await _client.AppendToStreamAsync(
                    stream,
                    options.ExpectedState,
                    chunk,
                    options.ConfigureOperation,
                    options.Credentials,
                    cancellationToken
                )
                .NoContext();
        }
    }

    EventData CreateMessage(object message, Type type, Metadata? metadata) {
        var msg = Ensure.NotNull(message, nameof(message));
        var (eventType, payload) = _serializer.SerializeEvent(msg);
        var metaBytes = metadata == null ? null : _metaSerializer.Serialize(metadata);

        return new EventData(
            Uuid.NewUuid(),
            eventType,
            payload,
            metaBytes,
            _serializer.ContentType
        );
    }
}