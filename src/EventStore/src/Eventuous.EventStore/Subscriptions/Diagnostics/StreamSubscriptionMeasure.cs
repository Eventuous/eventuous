using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.EventStore.Subscriptions.Diagnostics;

public class StreamSubscriptionMeasure : ISubscriptionGapMeasure {
    public StreamSubscriptionMeasure(
        EventStoreClient     eventStoreClient,
        bool                 resolveLinkTos,
        Func<EventPosition?> getLast
    ) {
        _eventStoreClient = eventStoreClient;
        _resolveLinkTos   = resolveLinkTos;
        _getLast          = getLast;
    }

    readonly EventStoreClient     _eventStoreClient;
    readonly bool                 _resolveLinkTos;
    readonly Func<EventPosition?> _getLast;

    public async Task<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken) {
        var read = _eventStoreClient.ReadAllAsync(
            Direction.Backwards,
            Position.End,
            1,
            resolveLinkTos: _resolveLinkTos,
            cancellationToken: cancellationToken
        );

        var events = await read.ToArrayAsync(cancellationToken).NoContext();
        var last   = _getLast();

        return new SubscriptionGap(
            events[0].Event.EventNumber - last?.Position ?? 0,
            DateTime.UtcNow - events[0].Event.Created
        );
    }
}