// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.EventStore.Subscriptions.Diagnostics;

class AllStreamSubscriptionMeasure(string subscriptionId, EventStoreClient eventStoreClient)
    : BaseSubscriptionMeasure(subscriptionId, "$all", eventStoreClient) {
    protected override IAsyncEnumerable<ResolvedEvent> Read(CancellationToken cancellationToken)
        => EventStoreClient.ReadAllAsync(Direction.Backwards, Position.End, 1, cancellationToken: cancellationToken);

    protected override ulong GetLastPosition(ResolvedEvent resolvedEvent) => resolvedEvent.Event.Position.CommitPosition;
}
