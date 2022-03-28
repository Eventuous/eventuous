using Eventuous.Subscriptions.Context;
using Nest;

namespace Eventuous.Connectors.EsdbElastic;

class ElasticMeta {
    public static Dictionary<string, string?>? FromMetadata(Metadata? metadata)
        => metadata?.ToDictionary(x => x.Key, x => x.Value?.ToString());
}

[ElasticsearchType(IdProperty = nameof(MessageId))]
[EventType("Event")]
record PersistedEvent(
    string                                         MessageId,
    [property: Keyword] string                     MessageType,
    long                                           StreamPosition,
    string                                         ContentType,
    string                                         Stream,
    ulong                                          GlobalPosition,
    object?                                        Message,
    Dictionary<string, string?>?                   Metadata,
    [property: Date(Name = "@timestamp")] DateTime Created
) {
    public static PersistedEvent From(IMessageConsumeContext ctx)
        => new(
            ctx.MessageId,
            ctx.MessageType,
            ctx.StreamPosition,
            ctx.ContentType,
            ctx.Stream,
            ctx.GlobalPosition,
            ctx.Message,
            ElasticMeta.FromMetadata(ctx.Metadata),
            ctx.Created
        );
}
