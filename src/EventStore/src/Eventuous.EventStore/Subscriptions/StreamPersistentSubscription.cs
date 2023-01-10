// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Persistent subscription for EventStoreDB, for a specific stream
/// </summary>
public class StreamPersistentSubscription
    : PersistentSubscriptionBase<StreamPersistentSubscriptionOptions>, IMeasuredSubscription {
    public StreamPersistentSubscription(
        EventStoreClient                    eventStoreClient,
        StreamPersistentSubscriptionOptions options,
        ConsumePipe                         consumePipe,
        ILoggerFactory?                     loggerFactory
    ) : base(eventStoreClient, options, consumePipe, loggerFactory)
        => Ensure.NotEmptyString(options.StreamName);

    /// <summary>
    /// Creates EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="consumerPipe"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="loggerFactory"></param>
    public StreamPersistentSubscription(
        EventStoreClient     eventStoreClient,
        StreamName           streamName,
        string               subscriptionId,
        ConsumePipe          consumerPipe,
        IEventSerializer?    eventSerializer = null,
        IMetadataSerializer? metaSerializer  = null,
        ILoggerFactory?      loggerFactory   = null
    ) : this(
        eventStoreClient,
        new StreamPersistentSubscriptionOptions {
            StreamName         = streamName,
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer
        },
        consumerPipe,
        loggerFactory
    ) { }

    protected override Task CreatePersistentSubscription(
        PersistentSubscriptionSettings settings,
        CancellationToken              cancellationToken
    )
        => SubscriptionClient.CreateAsync(
            Options.StreamName,
            Options.SubscriptionId,
            settings,
            Options.Deadline,
            Options.Credentials,
            cancellationToken
        );

    protected override Task<PersistentSubscription> LocalSubscribe(
        Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
        Action<PersistentSubscription, SubscriptionDroppedReason, Exception?>?     subscriptionDropped,
        CancellationToken                                                          cancellationToken
    )
        => SubscriptionClient.SubscribeToStreamAsync(
            Options.StreamName,
            Options.SubscriptionId,
            eventAppeared,
            subscriptionDropped,
            Options.Credentials,
            Options.BufferSize,
            cancellationToken
        );

    protected override ulong GetContextStreamPosition(ResolvedEvent re) => re.Event.EventNumber;

    public GetSubscriptionEndOfStream GetMeasure()
        => new StreamSubscriptionMeasure(Options.SubscriptionId, Options.StreamName, EventStoreClient)
            .GetEndOfStream;
}