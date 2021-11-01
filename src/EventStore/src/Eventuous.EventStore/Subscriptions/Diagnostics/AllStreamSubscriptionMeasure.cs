using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.EventStore.Subscriptions.Diagnostics; 

public class AllStreamSubscriptionMeasure : ISubscriptionGapMeasure {
    public AllStreamSubscriptionMeasure(EventStoreClient eventStoreClient, Func<EventPosition?> getLast) {
        _eventStoreClient = eventStoreClient;
        _getLast     = getLast;
    }

    readonly EventStoreClient     _eventStoreClient;
    readonly Func<EventPosition?> _getLast;

    public async Task<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken) {
        var read = _eventStoreClient.ReadAllAsync(
            Direction.Backwards,
            Position.End,
            1,
            cancellationToken: cancellationToken
        );

        var events = await read.ToArrayAsync(cancellationToken).NoContext();
        var last   = _getLast();

        return new SubscriptionGap(
            events[0].Event.Position.CommitPosition - last?.Position ?? 0,
            DateTime.UtcNow - events[0].Event.Created
        );
    }
}