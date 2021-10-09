namespace Eventuous.Subscriptions;

public record ReceivedEvent(
    string    EventId,
    string    EventType,
    string    ContentType,
    ulong     GlobalPosition,
    ulong     StreamPosition,
    string    Stream,
    ulong     Sequence,
    DateTime  Created,
    object?   Payload,
    Metadata? Metadata
);

public record ReceivedEvent<T>(
    string    EventId,
    string    EventType,
    string    ContentType,
    ulong     GlobalPosition,
    ulong     StreamPosition,
    string    Stream,
    ulong     Sequence,
    DateTime  Created,
    T         Payload,
    Metadata? Metadata
) {
    public ReceivedEvent(ReceivedEvent re)
        : this(
            re.EventId,
            re.EventType,
            re.ContentType,
            re.GlobalPosition,
            re.StreamPosition,
            re.Stream,
            re.Sequence,
            re.Created,
            (T)re.Payload!,
            re.Metadata
        ) { }
}