using System.Diagnostics;

namespace Eventuous.Subscriptions.Monitoring; 

public record UnprocessedEventsCount(string SubscriptionId, long Count);

public delegate ValueTask<UnprocessedEventsCount> GetUnprocessedEventCount(CancellationToken cancellationToken);

class Test {
    // DistributedContextPropagator
}