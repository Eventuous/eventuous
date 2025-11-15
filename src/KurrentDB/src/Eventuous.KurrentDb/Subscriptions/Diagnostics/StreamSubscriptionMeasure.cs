// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.KurrentDB.Subscriptions.Diagnostics;

class StreamSubscriptionMeasure(string subscriptionId, StreamName streamName, KurrentDBClient client)
    : BaseSubscriptionMeasure(subscriptionId, streamName, client) {
    protected override IAsyncEnumerable<ResolvedEvent> Read(CancellationToken cancellationToken)
        => Client.ReadStreamAsync(Direction.Backwards, streamName, StreamPosition.End, 1, cancellationToken: cancellationToken);

    protected override ulong GetLastPosition(ResolvedEvent resolvedEvent) => resolvedEvent.Event.EventNumber;
}
