using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

public abstract class WrappedConsumeContext : IMessageConsumeContext {
    protected IMessageConsumeContext InnerContext { get; }

    protected WrappedConsumeContext(IMessageConsumeContext innerContext) => InnerContext = innerContext;

    public string          MessageId       => InnerContext.MessageId;
    public string          MessageType     => InnerContext.MessageType;
    public string          ContentType     => InnerContext.ContentType;
    public StreamName      Stream          => InnerContext.Stream;
    public ulong           StreamPosition  => InnerContext.StreamPosition;
    public ulong           GlobalPosition  => InnerContext.GlobalPosition;
    public DateTime        Created         => InnerContext.Created;
    public object?         Message         => InnerContext.Message;
    public Metadata?       Metadata        => InnerContext.Metadata;
    public ContextItems    Items           => InnerContext.Items;
    public HandlingResults HandlingResults => InnerContext.HandlingResults;
    public ulong           Sequence        => InnerContext.Sequence;
    public string          SubscriptionId  => InnerContext.SubscriptionId;

    public CancellationToken CancellationToken {
        get => InnerContext.CancellationToken;
        set => InnerContext.CancellationToken = value;
    }

    public ActivityContext? ParentContext {
        get => InnerContext.ParentContext;
        set => InnerContext.ParentContext = value;
    }
}
