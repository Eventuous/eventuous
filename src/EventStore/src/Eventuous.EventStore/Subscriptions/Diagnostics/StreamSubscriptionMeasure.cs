using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Tools;

namespace Eventuous.EventStore.Subscriptions.Diagnostics;

class StreamSubscriptionMeasure : BaseSubscriptionMeasure {
    public StreamSubscriptionMeasure(
        string           subscriptionId,
        StreamName       streamName,
        EventStoreClient eventStoreClient
    ) : base(subscriptionId, streamName, eventStoreClient) {
        _subscriptionId = subscriptionId;
        _streamName     = streamName;
    }

    readonly string     _subscriptionId;
    readonly StreamName _streamName;

    protected override IAsyncEnumerable<ResolvedEvent> Read(CancellationToken cancellationToken)
        => _eventStoreClient.ReadStreamAsync(
            Direction.Backwards,
            _streamName,
            StreamPosition.End,
            1,
            cancellationToken: cancellationToken
        );

    protected override ulong GetLastPosition(ResolvedEvent resolvedEvent) => resolvedEvent.Event.EventNumber;
}