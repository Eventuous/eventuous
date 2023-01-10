// Copyright (C) Ubiquitous AS. All rights reserved
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
public class AllStreamSubscription
    : EventStoreCatchUpSubscriptionBase<AllStreamSubscriptionOptions>, IMeasuredSubscription {
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
        new AllStreamSubscriptionOptions {
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer,
            EventFilter        = eventFilter
        },
        checkpointStore,
        consumePipe,
        loggerFactory
    ) { }

    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="eventStoreClient"></param>
    /// <param name="options"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    public AllStreamSubscription(
        EventStoreClient             eventStoreClient,
        AllStreamSubscriptionOptions options,
        ICheckpointStore             checkpointStore,
        ConsumePipe                  consumePipe,
        ILoggerFactory?              loggerFactory
    ) : base(eventStoreClient, options, checkpointStore, consumePipe, loggerFactory) { }

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var filterOptions = new SubscriptionFilterOptions(
            Options.EventFilter ?? EventTypeFilter.ExcludeSystemEvents(),
            Options.CheckpointInterval,
            async (_, p, ct) => {
                // !!! Checkpointing is disabled as it comes out of sync with delayed events
                if (Options.ConcurrencyLimit > 1) return;

                // This doesn't allow to report tie time gap
                LastProcessed = new EventPosition(p.CommitPosition, DateTime.Now);
                await StoreCheckpoint(LastProcessed, ct).NoContext();
            }
        );

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        var fromAll = position == null ? FromAll.Start : FromAll.After(new Position(position.Value, position.Value));

        Subscription = await EventStoreClient.SubscribeToAllAsync(
                fromAll,
                (subscription, @event, ct) => HandleEvent(subscription, @event, ct),
                Options.ResolveLinkTos,
                HandleDrop,
                filterOptions,
                Options.Credentials,
                cancellationToken
            )
            .NoContext();

        async Task HandleEvent(
            global::EventStore.Client.StreamSubscription _,
            ResolvedEvent                                re,
            CancellationToken                            ct
        )
            => await HandleInternal(CreateContext(re, ct)).NoContext();

        void HandleDrop(
            global::EventStore.Client.StreamSubscription _,
            SubscriptionDroppedReason                    reason,
            Exception?                                   ex
        )
            => Dropped(EsdbMappings.AsDropReason(reason), ex);
    }

    IMessageConsumeContext CreateContext(ResolvedEvent re, CancellationToken cancellationToken) {
        var evt = DeserializeData(
            re.Event.ContentType,
            re.Event.EventType,
            re.Event.Data,
            re.Event.EventStreamId,
            re.Event.EventNumber
        );

        return new MessageConsumeContext(
            re.Event.EventId.ToString(),
            re.Event.EventType,
            re.Event.ContentType,
            re.OriginalStreamId,
            re.Event.EventNumber,
            re.Event.Position.CommitPosition,
            _sequence++,
            re.Event.Created,
            evt,
            Options.MetadataSerializer.DeserializeMeta(Options, re.Event.Metadata, re.OriginalStreamId),
            SubscriptionId,
            cancellationToken
        );
    }

    ulong _sequence;

    public GetSubscriptionEndOfStream GetMeasure()
        => new AllStreamSubscriptionMeasure(Options.SubscriptionId, EventStoreClient).GetEndOfStream;

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context)
        => EventPosition.FromAllContext(context);
}