namespace Eventuous.Subscriptions; 

[PublicAPI]
public class EventSubscription : ICanStop {
    readonly ICanStop _inner;

    public EventSubscription(string subscriptionId, ICanStop inner) {
        _inner         = inner;
        SubscriptionId = subscriptionId;
    }

    public string SubscriptionId { get; }

    public Task Stop(CancellationToken cancellationToken = default) => _inner.Stop(cancellationToken);
}
    
public class Stoppable : ICanStop {
    readonly Action _stop;

    public Stoppable(Action stop) => _stop = stop;

    public Task Stop(CancellationToken cancellationToken) {
        _stop();
        return Task.CompletedTask;
    }
}

public interface ICanStop {
    Task Stop(CancellationToken cancellationToken = default);
}