// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;

namespace Eventuous.EventStore.Subscriptions;

using Diagnostics;

/// <summary>
/// Catch-up subscription for EventStoreDB, for a specific stream
/// </summary>
[PublicAPI]
public class StreamSubscription : EventStoreCatchUpSubscriptionBase<StreamSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumerPipe">Consumer pipe instance</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    /// <param name="throwOnError">Either the subscription should throw an exception if an event handling fails</param>
    /// <param name="loggerFactory">Logger factory</param>
    public StreamSubscription(
            EventStoreClient     eventStoreClient,
            StreamName           streamName,
            string               subscriptionId,
            ICheckpointStore     checkpointStore,
            ConsumePipe          consumerPipe,
            bool                 throwOnError    = false,
            ILoggerFactory?      loggerFactory   = null,
            IEventSerializer?    eventSerializer = null,
            IMetadataSerializer? metaSerializer  = null
        )
        : this(
            eventStoreClient,
            new() {
                StreamName     = streamName,
                SubscriptionId = subscriptionId,
                ThrowOnError   = throwOnError
            },
            checkpointStore,
            consumerPipe,
            loggerFactory,
            eventSerializer,
            metaSerializer
        ) { }

    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for a given stream
    /// </summary>
    /// <param name="client"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    /// <param name="loggerFactory"></param>
    public StreamSubscription(
            EventStoreClient          client,
            StreamSubscriptionOptions options,
            ICheckpointStore          checkpointStore,
            ConsumePipe               consumePipe,
            ILoggerFactory?           loggerFactory   = null,
            IEventSerializer?         eventSerializer = null,
            IMetadataSerializer?      metaSerializer  = null
        ) : base(client, options, checkpointStore, consumePipe, SubscriptionKind.Stream, loggerFactory, eventSerializer, metaSerializer) {
        if (string.IsNullOrWhiteSpace(options.StreamName)) {
            Log.FatalLog?.Log("Subscription has no stream name configured. Use SubscriptionBuilder.Configure to set the stream name", SubscriptionId);

            // ReSharper disable once NotResolvedInText
#pragma warning disable CA2208
            throw new ArgumentNullException("StreamName");
#pragma warning restore CA2208
        }
    }

    /// <summary>
    /// Starts a catch-up subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        var fromStream = position == null ? FromStream.Start : FromStream.After(StreamPosition.FromInt64((long)position));

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
        Log.InfoLog?.Log("Subscribed to stream {Stream}", Options.StreamName);

        return;

        async Task HandleEvent(ResolvedEvent re, CancellationToken ct) {
            // Despite ResolvedEvent.Event being not marked as nullable, it returns null for deleted events
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (re.Event is null) return;

            if (Options.IgnoreSystemEvents && re.Event.EventType.Length > 0 && re.Event.EventType[0] == '$') return;

            await HandleInternal(CreateContext(re, ct)).NoContext();
        }

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

        var meta = MetadataSerializer.DeserializeMeta(
            Options,
            re.Event.Metadata,
            re.Event.EventStreamId,
            re.Event.EventNumber
        );

        return new(
            re.Event.EventId.ToString(),
            re.Event.EventType,
            re.Event.ContentType,
            re.Event.EventStreamId,
            re.Event.EventNumber,
            re.OriginalEventNumber.ToUInt64(),
            re.Event.Position.CommitPosition,
            Sequence++,
            re.Event.Created,
            evt,
            meta,
            SubscriptionId,
            cancellationToken
        );
    }

    /// <summary>
    /// Returns a measure delegate for this subscription
    /// </summary>
    /// <returns></returns>
    public GetSubscriptionEndOfStream GetMeasure()
        => new StreamSubscriptionMeasure(Options.SubscriptionId, Options.StreamName, EventStoreClient).GetEndOfStream;
}
