namespace Eventuous.Subscriptions.Context;

public delegate ValueTask Acknowledge(IMessageConsumeContext ctx);

public delegate ValueTask Fail(IMessageConsumeContext ctx, Exception exception);

public class DelayedAckConsumeContext : WrappedConsumeContext {
    readonly Acknowledge _acknowledge;
    readonly Fail        _fail;

    public DelayedAckConsumeContext(IMessageConsumeContext inner, Acknowledge acknowledge, Fail fail)
        : base(inner) {
        _acknowledge = acknowledge;
        _fail   = fail;
    }

    public ValueTask Acknowledge() => _acknowledge(this);

    public ValueTask Fail(Exception exception) => _fail(this, exception);
}