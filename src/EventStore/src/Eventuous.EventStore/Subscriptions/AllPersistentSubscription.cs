// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Persistent subscription for EventStoreDB, for $all stream
/// </summary>
public class AllPersistentSubscription : PersistentSubscriptionBase<AllPersistentSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Persistent subscription for EventStoreDB, for $all stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB client instance</param>
    /// <param name="options">Persistent subscription options</param>
    /// <param name="consumePipe">Consume pipe, usually provided by the builder</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer">Event payload serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    public AllPersistentSubscription(
            EventStoreClient                 eventStoreClient,
            AllPersistentSubscriptionOptions options,
            ConsumePipe                      consumePipe,
            ILoggerFactory?                  loggerFactory   = null,
            IEventSerializer?                eventSerializer = null,
            IMetadataSerializer?             metaSerializer  = null
        )
        : base(eventStoreClient, options, consumePipe, loggerFactory, eventSerializer, metaSerializer) { }

    /// <summary>
    /// Persistent subscription for EventStoreDB, for $all stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB persistent subscription client instance</param>
    /// <param name="options">Persistent subscription options</param>
    /// <param name="consumePipe">Consume pipe, usually provided by the builder</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer">Event payload serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    public AllPersistentSubscription(
            EventStorePersistentSubscriptionsClient eventStoreClient,
            AllPersistentSubscriptionOptions        options,
            ConsumePipe                             consumePipe,
            ILoggerFactory?                         loggerFactory   = null,
            IEventSerializer?                       eventSerializer = null,
            IMetadataSerializer?                    metaSerializer  = null
        )
        : base(eventStoreClient, options, consumePipe, loggerFactory, eventSerializer, metaSerializer) { }

    /// <summary>
    /// Creates EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="consumerPipe">Consume pipe</param>
    /// <param name="eventSerializer">Optional: event serializer instance</param>
    /// <param name="metaSerializer">Optional: metadata serializer instance</param>
    /// <param name="eventFilter">Optional: subscription filter</param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    public AllPersistentSubscription(
            EventStoreClient     eventStoreClient,
            string               subscriptionId,
            ConsumePipe          consumerPipe,
            IEventSerializer?    eventSerializer = null,
            IMetadataSerializer? metaSerializer  = null,
            IEventFilter?        eventFilter     = null,
            ILoggerFactory?      loggerFactory   = null
        )
        : this(
            eventStoreClient,
            new() {
                SubscriptionId = subscriptionId,
                EventFilter    = eventFilter
            },
            consumerPipe,
            loggerFactory,
            eventSerializer,
            metaSerializer
        ) { }

    /// <summary>
    /// Creates EventStoreDB persistent subscription consumer group for $all
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override Task CreatePersistentSubscription(PersistentSubscriptionSettings settings, CancellationToken cancellationToken)
        => SubscriptionClient.CreateToAllAsync(
            Options.SubscriptionId,
            Options.EventFilter!, // although the argument is not nullable, it calls an internal method that accepts null
            settings,
            Options.Deadline,
            Options.Credentials,
            cancellationToken
        );

    /// <summary>
    /// Internal method to start the subscription
    /// </summary>
    /// <param name="eventAppeared">Event processing delegate</param>
    /// <param name="subscriptionDropped">Drop handler</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override Task<PersistentSubscription> LocalSubscribe(
            Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
            Action<PersistentSubscription, SubscriptionDroppedReason, Exception?>?     subscriptionDropped,
            CancellationToken                                                          cancellationToken
        )
        => SubscriptionClient.SubscribeToAllAsync(
            Options.SubscriptionId,
            eventAppeared,
            subscriptionDropped,
            Options.Credentials,
            Options.BufferSize,
            cancellationToken
        );

    /// <inheritdoc />
    protected override ulong GetContextStreamPosition(ResolvedEvent re) => re.Event.Position.CommitPosition;

    /// <summary>
    /// Returns a measure callback for the subscription
    /// </summary>
    /// <returns></returns>
    public GetSubscriptionEndOfStream GetMeasure() => new AllStreamSubscriptionMeasure(Options.SubscriptionId, EventStoreClient).GetEndOfStream;
}
