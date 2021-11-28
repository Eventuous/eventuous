using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.EventStore.Subscriptions.Diagnostics;

class StreamSubscriptionMeasure {
    public StreamSubscriptionMeasure(
        string               subscriptionId,
        StreamName           streamName,
        EventStoreClient     eventStoreClient,
        Func<EventPosition?> getLast
    ) {
        _subscriptionId   = subscriptionId;
        _streamName       = streamName;
        _eventStoreClient = eventStoreClient;
        _getLast          = getLast;
    }

    readonly string               _subscriptionId;
    readonly StreamName           _streamName;
    readonly EventStoreClient     _eventStoreClient;
    readonly Func<EventPosition?> _getLast;

    public async ValueTask<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken) {
        var read = _eventStoreClient.ReadStreamAsync(
            Direction.Backwards,
            _streamName,
            StreamPosition.End,
            1,
            cancellationToken: cancellationToken
        );

        try {
            var events = await read.ToArrayAsync(cancellationToken).NoContext();
            var last   = _getLast()?.Position ?? 0;

            return new SubscriptionGap(
                _subscriptionId,
                events[0].Event.EventNumber - last,
                DateTime.UtcNow             - events[0].Event.Created
            );
        }
        catch (StreamNotFoundException) {
            return new SubscriptionGap(_subscriptionId, 0, TimeSpan.Zero);
        }
    }
}
