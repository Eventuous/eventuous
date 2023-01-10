// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Catch-up subscription for EventStoreDB, for a specific stream
/// </summary>
[PublicAPI]
public class StreamSubscription
    : EventStoreCatchUpSubscriptionBase<StreamSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumerPipe"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="throwOnError"></param>
    /// <param name="loggerFactory"></param>
    public StreamSubscription(
        EventStoreClient     eventStoreClient,
        StreamName           streamName,
        string               subscriptionId,
        ICheckpointStore     checkpointStore,
        ConsumePipe          consumerPipe,
        IEventSerializer?    eventSerializer = null,
        IMetadataSerializer? metaSerializer  = null,
        bool                 throwOnError    = false,
        ILoggerFactory?      loggerFactory   = null
    ) : this(
        eventStoreClient,
        new StreamSubscriptionOptions {
            StreamName         = streamName,
            SubscriptionId     = subscriptionId,
            ThrowOnError       = throwOnError,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer
        },
        checkpointStore,
        consumerPipe,
        loggerFactory
    ) { }

    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for a given stream
    /// </summary>
    /// <param name="client"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    public StreamSubscription(
        EventStoreClient          client,
        StreamSubscriptionOptions options,
        ICheckpointStore          checkpointStore,
        ConsumePipe               consumePipe,
        ILoggerFactory?           loggerFactory = null
    ) : base(client, options, checkpointStore, consumePipe, loggerFactory)
        => Ensure.NotEmptyString(options.StreamName);

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        var fromStream = position == null ? FromStream.Start
            : FromStream.After(StreamPosition.FromInt64((long)position));

        Subscription = await EventStoreClient.SubscribeToStreamAsync(
                Options.StreamName,
                fromStream,
                (_, @event, ct) => HandleEvent(@event, ct),
                Options.ResolveLinkTos,
                HandleDrop,
                Options.Credentials,
                cancellationToken
            )
            .NoContext();

        async Task HandleEvent(
            ResolvedEvent     re,
            CancellationToken ct
        ) {
            // Despite ResolvedEvent.Event being not marked as nullable, it returns null for deleted events
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (re.Event is null) return;

            if (Options.IgnoreSystemEvents && re.Event.EventType.Length > 0 && re.Event.EventType[0] == '$') return;

            await HandleInternal(CreateContext(re, ct)).NoContext();
        }

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
            re.Event.EventStreamId,
            re.OriginalEventNumber,
            re.Event.Position.CommitPosition,
            _sequence++,
            re.Event.Created,
            evt,
            Options.MetadataSerializer.DeserializeMeta(
                Options,
                re.Event.Metadata,
                re.OriginalStreamId,
                re.Event.EventNumber
            ),
            SubscriptionId,
            cancellationToken
        );
    }

    ulong _sequence;

    public GetSubscriptionEndOfStream GetMeasure()
        => new StreamSubscriptionMeasure(Options.SubscriptionId, Options.StreamName, EventStoreClient)
            .GetEndOfStream;

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context)
        => EventPosition.FromContext(context);
}