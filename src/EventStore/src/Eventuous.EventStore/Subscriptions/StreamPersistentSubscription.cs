// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Persistent subscription for EventStoreDB, for a specific stream
/// </summary>
public class StreamPersistentSubscription : PersistentSubscriptionBase<StreamPersistentSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB client instance</param>
    /// <param name="options">Persistent subscription options <see cref="StreamPersistentSubscriptionOptions"/></param>
    /// <param name="consumePipe">Consume pipe, provided automatically</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    public StreamPersistentSubscription(
            EventStoreClient                    eventStoreClient,
            StreamPersistentSubscriptionOptions options,
            ConsumePipe                         consumePipe,
            ILoggerFactory?                     loggerFactory = null,
            IEventSerializer?                   eventSerializer = null,
            IMetadataSerializer?                metaSerializer = null
        ) : base(eventStoreClient, options, consumePipe, loggerFactory, eventSerializer, metaSerializer)
        => Ensure.NotEmptyString(options.StreamName);

    /// <summary>
    /// EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB persistent subscription client instance</param>
    /// <param name="options">Persistent subscription options <see cref="StreamPersistentSubscriptionOptions"/></param>
    /// <param name="consumePipe">Consume pipe, provided automatically</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    public StreamPersistentSubscription(
            EventStorePersistentSubscriptionsClient eventStoreClient,
            StreamPersistentSubscriptionOptions     options,
            ConsumePipe                             consumePipe,
            ILoggerFactory?                         loggerFactory = null,
            IEventSerializer?                       eventSerializer = null,
            IMetadataSerializer?                    metaSerializer = null
        ) : base(eventStoreClient, options, consumePipe, loggerFactory, eventSerializer, metaSerializer)
        => Ensure.NotEmptyString(options.StreamName);

    /// <summary>
    /// Creates EventStoreDB persistent subscription service for a given stream without using the options object
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
        new() {
            StreamName     = streamName,
            SubscriptionId = subscriptionId
        },
        consumerPipe,
        loggerFactory,
        eventSerializer,
        metaSerializer
    ) { }

    /// <inheritdoc/>
    protected override Task CreatePersistentSubscription(PersistentSubscriptionSettings settings, CancellationToken cancellationToken)
        => SubscriptionClient.CreateToStreamAsync(
            Options.StreamName,
            Options.SubscriptionId,
            settings,
            Options.Deadline,
            Options.Credentials,
            cancellationToken
        );

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    protected override ulong GetContextStreamPosition(ResolvedEvent re) => re.Event.EventNumber;

    /// <inheritdoc/>
    public GetSubscriptionEndOfStream GetMeasure()
        => new StreamSubscriptionMeasure(Options.SubscriptionId, Options.StreamName, EventStoreClient).GetEndOfStream;
}
