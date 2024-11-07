// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;

// ReSharper disable ConvertClosureToMethodGroup

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Catch-up subscription for EventStoreDB, using the $all global stream
/// </summary>
[PublicAPI]
public class AllStreamSubscription : EventStoreCatchUpSubscriptionBase<AllStreamSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumePipe"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="eventFilter">Optional: server-side event filter</param>
    /// <param name="loggerFactory"></param>
    public AllStreamSubscription(
            EventStoreClient     eventStoreClient,
            string               subscriptionId,
            ICheckpointStore     checkpointStore,
            ConsumePipe          consumePipe,
            IEventSerializer?    eventSerializer = null,
            IMetadataSerializer? metaSerializer  = null,
            IEventFilter?        eventFilter     = null,
            ILoggerFactory?      loggerFactory   = null
        ) : this(
        eventStoreClient,
        new() {
            SubscriptionId = subscriptionId,
            EventFilter    = eventFilter
        },
        checkpointStore,
        consumePipe,
        loggerFactory,
        eventSerializer,
        metaSerializer
    ) { }

    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="eventStoreClient"></param>
    /// <param name="options"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    public AllStreamSubscription(
            EventStoreClient             eventStoreClient,
            AllStreamSubscriptionOptions options,
            ICheckpointStore             checkpointStore,
            ConsumePipe                  consumePipe,
            ILoggerFactory?              loggerFactory   = null,
            IEventSerializer?            eventSerializer = null,
            IMetadataSerializer?         metaSerializer  = null
        ) : base(eventStoreClient, options, checkpointStore, consumePipe, SubscriptionKind.All, loggerFactory, eventSerializer, metaSerializer) { }

    /// <summary>
    /// Starts the subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var filterOptions = new SubscriptionFilterOptions(
            Options.EventFilter ?? EventTypeFilter.ExcludeSystemEvents(),
            Options.CheckpointInterval
        );

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        var fromAll = position == null ? FromAll.Start : FromAll.After(new Position(position.Value, position.Value));

        Subscription = await EventStoreClient.SubscribeToAllAsync(
                fromAll,
                (_, @event, ct) => HandleEvent(@event, ct),
                Options.ResolveLinkTos,
                HandleDrop,
                filterOptions,
                Options.Credentials,
                cancellationToken
            )
            .NoContext();

        return;

        Task HandleEvent(ResolvedEvent re, CancellationToken ct)
            => HandleInternal(CreateContext(re, ct)).AsTask();

        void HandleDrop(global::EventStore.Client.StreamSubscription _, SubscriptionDroppedReason reason, Exception? ex)
            => Dropped(EsdbMappings.AsDropReason(reason), ex);
    }

    MessageConsumeContext CreateContext(ResolvedEvent re, CancellationToken cancellationToken) {
        var evt = DeserializeData(
            re.Event.ContentType,
            re.Event.EventType,
            re.Event.Data,
            re.Event.EventStreamId,
            re.Event.EventNumber
        );

        return new(
            re.Event.EventId.ToString(),
            re.Event.EventType,
            re.Event.ContentType,
            re.Event.EventStreamId,
            re.Event.EventNumber,
            re.OriginalEventNumber,
            re.Event.Position.CommitPosition,
            Sequence++,
            re.Event.Created,
            evt,
            MetadataSerializer.DeserializeMeta(Options, re.Event.Metadata, re.Event.EventStreamId),
            SubscriptionId,
            cancellationToken
        );
    }

    /// <summary>
    /// Returns a measure delegate for the subscription
    /// </summary>
    /// <returns></returns>
    public GetSubscriptionEndOfStream GetMeasure() => new AllStreamSubscriptionMeasure(Options.SubscriptionId, EventStoreClient).GetEndOfStream;
}
