using Eventuous.Subscriptions.Consumers;

namespace Eventuous.Subscriptions.Context;

public delegate ValueTask Acknowledge(CancellationToken cancellationToken);

public class DelayedAckConsumeContext : WrappedContext {
    public Acknowledge Acknowledge { get; }

    public DelayedAckConsumeContext(Acknowledge acknowledge, IMessageConsumeContext inner) : base(inner) {
        Acknowledge = acknowledge;
    }
}