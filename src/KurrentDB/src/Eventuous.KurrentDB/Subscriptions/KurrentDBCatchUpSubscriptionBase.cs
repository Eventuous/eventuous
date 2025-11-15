// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.KurrentDB.Subscriptions;

/// <summary>
/// Base class for EventStoreDB catch-up subscriptions
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public abstract class KurrentDBCatchUpSubscriptionBase<T> : EventSubscriptionWithCheckpoint<T> where T : CatchUpSubscriptionOptions {
    /// <summary>
    /// Catch-up subscription base class constructor
    /// </summary>
    /// <param name="client">EventStoreDB client instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="checkpointStore">Checkpoint store</param>
    /// <param name="consumePipe">Consume pipe, usually provided by the subscription builder</param>
    /// <param name="kind">Subscription kind: global log or a particular stream</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer">Optional: event serializer instance</param>
    /// <param name="metaSerializer">Optional: metadata serializer instance</param>
    protected KurrentDBCatchUpSubscriptionBase(
            KurrentDBClient      client,
            T                    options,
            ICheckpointStore     checkpointStore,
            ConsumePipe          consumePipe,
            SubscriptionKind     kind,
            ILoggerFactory?      loggerFactory,
            IEventSerializer?    eventSerializer,
            IMetadataSerializer? metaSerializer
        )
        : base(Ensure.NotNull(options), checkpointStore, consumePipe, options.ConcurrencyLimit, kind, loggerFactory, eventSerializer, metaSerializer)
        => Client = client;

    /// <summary>
    /// EventStoreDB client instance
    /// </summary>
    protected KurrentDBClient Client { get; }

    /// <summary>
    /// Stops the subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            Stopping.Cancel(false);
            Subscription?.Dispose();
            await Task.Delay(100, cancellationToken);
        } catch (Exception) {
            // Nothing to see here
        }
    }

    /// <summary>
    /// Underlying EventStoreDB subscription
    /// </summary>
    protected global::KurrentDB.Client.StreamSubscription? Subscription { get; set; }
}
