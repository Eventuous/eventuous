// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract class EventStoreCatchUpSubscriptionBase<T> : EventSubscriptionWithCheckpoint<T>
    where T : CatchUpSubscriptionOptions {

    protected EventStoreCatchUpSubscriptionBase(
        EventStoreClient eventStoreClient,
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        ILoggerFactory?  loggerFactory
    ) : base(Ensure.NotNull(options), checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory)
        => EventStoreClient = eventStoreClient;

    protected EventStoreClient EventStoreClient { get; }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            Stopping.Cancel(false);
            await Task.Delay(100, cancellationToken);
            Subscription?.Dispose();
        }
        catch (Exception) {
            // Nothing to see here
        }
    }

    protected global::EventStore.Client.StreamSubscription? Subscription { get; set; }
}