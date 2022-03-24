using Eventuous.Subscriptions.Context;
using Nest;

namespace Eventuous.ElasticSearch;

[ElasticsearchType(IdProperty = nameof(EventId))]
public record PersistedEvent(
    string EventId,
    [property: Keyword] string EventType,
    long StreamPosition,
    string ContentType,
    string Stream,
    ulong GlobalPosition,
    object? Payload,
    [property: Date(Name = "@timestamp")] DateTime Created)
{
    public static PersistedEvent From(IMessageConsumeContext ctx) =>
        new(ctx.MessageId,
            ctx.MessageType,
            ctx.StreamPosition,
            ctx.ContentType,
            ctx.Stream,
            ctx.GlobalPosition,
            ctx.Message,
            ctx.Created);
}