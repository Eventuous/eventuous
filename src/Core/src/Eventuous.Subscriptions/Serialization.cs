using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions;

public static class SubscriptionSerialization {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? DeserializeSubscriptionPayload(
        this IEventSerializer serializer,
        string                eventContentType,
        string                eventType,
        ReadOnlyMemory<byte>  data,
        string                stream,
        ulong                 position = 0
    ) {
        if (data.IsEmpty) return null;

        var contentType = string.IsNullOrWhiteSpace(eventType) ? "application/json"
            : eventContentType;

        if (contentType != serializer.ContentType) {
            throw new DeserializationException(stream, eventType, position, $"Unknown content type {contentType}");
        }

        try {
            return serializer.DeserializeEvent(data.Span, eventType);
        }
        catch (Exception e) {
            throw new DeserializationException(stream, eventType, position, e);
        }
    }
}