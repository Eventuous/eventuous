using System;

namespace Eventuous.Subscriptions {
    public record ReceivedEvent(
        string               EventId,
        string               EventType,
        string               ContentType,
        ulong                GlobalPosition,
        ulong                StreamPosition,
        string               Stream,
        ulong                Sequence,
        DateTime             Created,
        object?              Payload
        // ReadOnlyMemory<byte> Metadata
        
        // TODO Add meta with serialization
    );
}