namespace Eventuous.EventStore.Subscriptions.Diagnostics;

class AllStreamSubscriptionMeasure : BaseSubscriptionMeasure {
    public AllStreamSubscriptionMeasure(string subscriptionId, EventStoreClient eventStoreClient)
        : base(subscriptionId, "$all", eventStoreClient) { }

    protected override IAsyncEnumerable<ResolvedEvent> Read(CancellationToken cancellationToken)
        => _eventStoreClient.ReadAllAsync(
            Direction.Backwards,
            Position.End,
            1,
            cancellationToken: cancellationToken
        );

    protected override ulong GetLastPosition(ResolvedEvent resolvedEvent)
        => resolvedEvent.Event.Position.CommitPosition;
}