namespace Eventuous.Subscriptions.Context;

public delegate ValueTask Acknowledge(CancellationToken cancellationToken);

public delegate ValueTask Fail(Exception exception, CancellationToken cancellationToken);

public class DelayedAckConsumeContext : WrappedConsumeContext {
    readonly Acknowledge _acknowledge;
    readonly Fail        _fail;

    public DelayedAckConsumeContext(IMessageConsumeContext inner, Acknowledge acknowledge, Fail fail)
        : base(inner) {
        _acknowledge = acknowledge;
        _fail   = fail;
    }

    public ValueTask Acknowledge() => _acknowledge(CancellationToken);

    public ValueTask Fail(Exception exception) => _fail(exception, CancellationToken);
}