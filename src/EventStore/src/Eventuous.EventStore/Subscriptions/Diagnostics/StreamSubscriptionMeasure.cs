namespace Eventuous.EventStore.Subscriptions.Diagnostics;

class StreamSubscriptionMeasure(string subscriptionId, StreamName streamName, EventStoreClient eventStoreClient)
    : BaseSubscriptionMeasure(subscriptionId, streamName, eventStoreClient) {
    protected override IAsyncEnumerable<ResolvedEvent> Read(CancellationToken cancellationToken)
        => EventStoreClient.ReadStreamAsync(Direction.Backwards, streamName, StreamPosition.End, 1, cancellationToken: cancellationToken);

    protected override ulong GetLastPosition(ResolvedEvent resolvedEvent) => resolvedEvent.Event.EventNumber;
}
