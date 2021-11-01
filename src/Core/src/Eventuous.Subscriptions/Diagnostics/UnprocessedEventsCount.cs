namespace Eventuous.Subscriptions.Diagnostics; 

public record UnprocessedEventsCount(string SubscriptionId, long Count);

public delegate ValueTask<UnprocessedEventsCount> GetUnprocessedEventCount(CancellationToken cancellationToken);

class Test {
    // DistributedContextPropagator
}