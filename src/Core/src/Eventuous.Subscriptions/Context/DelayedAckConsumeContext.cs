namespace Eventuous.Subscriptions.Context;

public delegate ValueTask Acknowledge(CancellationToken cancellationToken);

public class DelayedAckConsumeContext : WrappedConsumeContext {
    readonly Acknowledge _acknowledge;

    public DelayedAckConsumeContext(Acknowledge acknowledge, IMessageConsumeContext inner)
        : base(inner)
        => _acknowledge = acknowledge;

    public ValueTask Acknowledge(CancellationToken cancellationToken) 
        => _acknowledge(cancellationToken);
}