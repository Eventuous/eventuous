// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;

namespace Eventuous.KurrentDB.Subscriptions;

using Diagnostics;

/// <summary>
/// Catch-up subscription for EventStoreDB, for a specific stream
/// </summary>
[PublicAPI]
public class StreamSubscription : KurrentDBCatchUpSubscriptionBase<StreamSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for a given stream
    /// </summary>
    /// <param name="client">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumerPipe">Consumer pipe instance</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    /// <param name="throwOnError">Either the subscription should throw an exception if an event handling fails</param>
    /// <param name="loggerFactory">Logger factory</param>
    public StreamSubscription(
            KurrentDBClient      client,
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
            client,
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
            KurrentDBClient           client,
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
    protected override async ValueTask Connect(SubscriptionRun run) {
        var (_, position) = await GetCheckpoint(run).NoContext();

        var fromStream = GetStreamPosition();

        var subscription = await Client.SubscribeToStreamAsync(
                Options.StreamName,
                fromStream,
                (_, @event, ct) => HandleEvent(@event, ct),
                Options.ResolveLinkTos,
                HandleDrop,
                Options.Credentials,
                run.Token
            )
            .NoContext();
        Log.InfoLog?.Log("Subscribed to stream {Stream}", Options.StreamName);

        // No settling delay needed: the supervisor now ignores failures raised while shutting down.
        run.OnDisconnect(_ => { subscription.Dispose(); return default; });

        return;

        FromStream GetStreamPosition() => position switch {
            null when Options.StartFrom == InitialPosition.Latest => FromStream.End,
            null                                                  => FromStream.Start,
            _                                                     => FromStream.After(StreamPosition.FromInt64((long)position))
        };

        async Task HandleEvent(ResolvedEvent re, CancellationToken ct) {
            // Despite ResolvedEvent.Event being not marked as nullable, it returns null for deleted events
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (re.Event is null) return;

            if (Options.IgnoreSystemEvents && re.Event.EventType.Length > 0 && re.Event.EventType[0] == '$') return;

            await HandleInternal(run, CreateContext(run, re, ct)).NoContext();
        }

        void HandleDrop(global::KurrentDB.Client.StreamSubscription _, SubscriptionDroppedReason reason, Exception? ex)
            => run.Fail(KurrentDBMappings.AsDropReason(reason), ex);
    }

    MessageConsumeContext CreateContext(SubscriptionRun run, ResolvedEvent re, CancellationToken cancellationToken) {
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
            run.NextSequence(),
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
        => new StreamSubscriptionMeasure(Options.SubscriptionId, Options.StreamName, Client).GetEndOfStream;
}
