// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.KurrentDB.Subscriptions.Diagnostics;

class AllStreamSubscriptionMeasure(string subscriptionId, KurrentDBClient client)
    : BaseSubscriptionMeasure(subscriptionId, "$all", client) {
    protected override IAsyncEnumerable<ResolvedEvent> Read(CancellationToken cancellationToken)
        => Client.ReadAllAsync(Direction.Backwards, Position.End, 1, cancellationToken: cancellationToken);

    protected override ulong GetLastPosition(ResolvedEvent resolvedEvent) => resolvedEvent.Event.Position.CommitPosition;
}
