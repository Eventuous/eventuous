using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

public class MessageConsumeContext : IMessageConsumeContext {
    public MessageConsumeContext(
        string    eventId,
        string    eventType,
        string    contentType,
        string    stream,
        ulong     sequence,
        DateTime  created,
        object?   message,
        Metadata? metadata
    ) {
        EventId     = eventId;
        EventType   = eventType;
        ContentType = contentType;
        Stream      = stream;
        Created     = created;
        Metadata    = metadata;
        Sequence    = sequence;
        Message     = message;
    }

    public string           EventId        { get; }
    public string           EventType      { get; }
    public string           ContentType    { get; }
    public string           Stream         { get; }
    public DateTime         Created        { get; }
    public Metadata?        Metadata       { get; }
    public object?          Message        { get; }
    public ContextItems     Items          { get; } = new();
    public ActivityContext? ParentContext  { get; set; }
    public ulong            Sequence       { get; init; }
    public ulong            GlobalPosition { get; init; }
    public ulong            StreamPosition { get; init; }
}

public class MessageConsumeContext<T> : WrappedConsumeContext, IMessageConsumeContext<T>
    where T : class {
    public MessageConsumeContext(IMessageConsumeContext innerContext) : base(innerContext) { }

    [PublicAPI]
    public new T? Message => (T?)InnerContext.Message;
}