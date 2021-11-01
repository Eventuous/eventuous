namespace Eventuous.Subscriptions.Context;

public class BaseConsumeContext {
    protected BaseConsumeContext(
        string    eventId,
        string    eventType,
        string    contentType,
        string    stream,
        DateTime  created,
        Metadata? metadata
    ) {
        EventId     = eventId;
        EventType   = eventType;
        ContentType = contentType;
        Stream      = stream;
        Created     = created;
        Metadata    = metadata;
    }

    public string    EventId     { get; }
    public string    EventType   { get; }
    public string    ContentType { get; }
    public string    Stream      { get; }
    public DateTime  Created     { get; }
    public Metadata? Metadata    { get; }
}