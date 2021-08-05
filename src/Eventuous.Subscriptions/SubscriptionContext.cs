using System;

namespace Eventuous.Subscriptions {
    public class SubscriptionContext {
        public string          EventId        { get; init; }
        public string          EventType      { get; init; }
        public string          ContentType    { get; init; }
        public string          Stream         { get; init; }
        public long            Sequence       { get; init; }
        public DateTime        Created        { get; init; }
        public StreamPosition? GlobalPosition { get; init; }
        public StreamPosition? StreamPosition { get; init; }
        public object?         Payload        { get; init; }
        public EventMetadata?  Metadata       { get; init; }
    }

    public record StreamPosition(ulong Value);
}