using Eventuous.Connectors.Base;
using Nest;

namespace Eventuous.Connectors.EsdbElastic.Conversions;

class ElasticMeta {
    public static Dictionary<string, string?>? FromMetadata(Metadata? metadata)
        => metadata?.ToDictionary(x => x.Key, x => x.Value?.ToString());
}

[ElasticsearchType(IdProperty = "MessageId")]
[EventType("Event")]
record PersistedEvent(
    string                                         MessageId,
    MessageType                                    MessageType,
    long                                           StreamPosition,
    string                                         ContentType,
    string                                         Stream,
    ulong                                          GlobalPosition,
    object?                                        Message,
    Dictionary<string, string?>?                   Metadata,
    [property: Date(Name = "@timestamp")] DateTime Created
);
