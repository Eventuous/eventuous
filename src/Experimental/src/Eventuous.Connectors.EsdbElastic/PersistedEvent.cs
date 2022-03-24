using Eventuous.Subscriptions.Context;
using Nest;

namespace Eventuous.Connectors.EsdbElastic;

[ElasticsearchType(IdProperty = nameof(MessageId))]
public record PersistedEvent(
    string                                         MessageId,
    [property: Keyword] string                     MessageType,
    long                                           StreamPosition,
    string                                         ContentType,
    string                                         Stream,
    ulong                                          GlobalPosition,
    object?                                        Message,
    Metadata?                                      Metadata,
    [property: Date(Name = "@timestamp")] DateTime Created
) {
    public static PersistedEvent From(IMessageConsumeContext ctx) =>
        new(
            ctx.MessageId,
            ctx.MessageType,
            ctx.StreamPosition,
            ctx.ContentType,
            ctx.Stream,
            ctx.GlobalPosition,
            ctx.Message,
            ctx.Metadata,
            ctx.Created
        );
}