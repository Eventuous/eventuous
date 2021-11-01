using Eventuous.Subscriptions.Consumers;

namespace Eventuous.Subscriptions.Context;

public class MessageConsumeContext : BaseConsumeContext, IMessageConsumeContext {
    public MessageConsumeContext(
        string    eventId,
        string    eventType,
        string    contentType,
        string    stream,
        ulong     sequence,
        DateTime  created,
        object?   message,
        Metadata? metadata
    ) : base(eventId, eventType, contentType, stream, created, metadata) {
        Sequence = sequence;
        Message  = message;
    }

    public object?      Message        { get; }
    public ContextItems Items          { get; } = new();
    public ulong        Sequence       { get; init; }
    public ulong        GlobalPosition { get; init; }
    public ulong        StreamPosition { get; init; }
}

public class MessageConsumeContext<T> : WrappedContext, IMessageConsumeContext<T> where T : class {
    public MessageConsumeContext(IMessageConsumeContext innerContext) : base(innerContext) { }

    [PublicAPI]
    public new T? Message => (T?)InnerContext.Message;
}