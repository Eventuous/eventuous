using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers; 

public abstract class WrappedContext : IMessageConsumeContext {
    protected   IMessageConsumeContext InnerContext { get; }

    protected WrappedContext(IMessageConsumeContext innerContext) => InnerContext = innerContext;

    public string       EventId     => InnerContext.EventId;
    public string       EventType   => InnerContext.EventType;
    public string       ContentType => InnerContext.ContentType;
    public string       Stream      => InnerContext.Stream;
    public DateTime     Created     => InnerContext.Created;
    public object?      Message     => InnerContext.Message;
    public Metadata?    Metadata    => InnerContext.Metadata;
    public ContextItems Items       => InnerContext.Items;
}