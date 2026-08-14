// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.KurrentDB.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;

namespace Eventuous.KurrentDB.Subscriptions;

/// <summary>
/// Catch-up subscription for EventStoreDB, using the $all global stream
/// </summary>
[PublicAPI]
public class AllStreamSubscription : KurrentDBCatchUpSubscriptionBase<AllStreamSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="client">EventStoreDB gRPC client instance</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumePipe"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="eventFilter">Optional: server-side event filter</param>
    /// <param name="loggerFactory"></param>
    public AllStreamSubscription(
            KurrentDBClient      client,
            string               subscriptionId,
            ICheckpointStore     checkpointStore,
            ConsumePipe          consumePipe,
            IEventSerializer?    eventSerializer = null,
            IMetadataSerializer? metaSerializer  = null,
            IEventFilter?        eventFilter     = null,
            ILoggerFactory?      loggerFactory   = null
        ) : this(
        client,
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
    /// <param name="client"></param>
    /// <param name="options"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    public AllStreamSubscription(
            KurrentDBClient              client,
            AllStreamSubscriptionOptions options,
            ICheckpointStore             checkpointStore,
            ConsumePipe                  consumePipe,
            ILoggerFactory?              loggerFactory   = null,
            IEventSerializer?            eventSerializer = null,
            IMetadataSerializer?         metaSerializer  = null
        ) : base(client, options, checkpointStore, consumePipe, SubscriptionKind.All, loggerFactory, eventSerializer, metaSerializer) { }

    /// <summary>
    /// Message type used for the synthetic, payload-less context created when the server reports
    /// a checkpoint position for a filtered subscription that hasn't matched any event in a while.
    /// This lets the checkpoint advance past long unmatched stretches instead of parking at the
    /// last matched event.
    /// </summary>
    internal const string CheckpointReachedMessageType = "$checkpoint-reached";

    /// <summary>
    /// Starts the subscription
    /// </summary>
    protected override async ValueTask Connect(SubscriptionRun run) {
        var filterOptions = new SubscriptionFilterOptions(Options.EventFilter ?? EventTypeFilter.ExcludeSystemEvents(), Options.CheckpointInterval);

        var (_, position) = await GetCheckpoint(run).NoContext();

        // The $all head, read before subscribing: by the time the server reports the subscription as
        // caught up, everything at or below this position has provably been scanned, so it can be
        // committed even if no event or checkpoint message ever surfaced it (small stores never cross
        // the checkpoint interval, idle tails park up to one interval below the head).
        var head = await GetAllStreamHead(run.Token).NoContext();

        var fromAll = GetPosition();

        var subscription = Client.SubscribeToAll(fromAll, Options.ResolveLinkTos, filterOptions, Options.Credentials, run.Token);
        var messages     = subscription.Messages.GetAsyncEnumerator(run.Token);

        try {
            if (!await messages.MoveNextAsync().NoContext() || messages.Current is not StreamMessage.SubscriptionConfirmation) {
                throw new InvalidOperationException($"Subscription {Options.SubscriptionId} to $all could not be confirmed");
            }
        } catch {
            await messages.DisposeAsync().NoContext();
            await subscription.DisposeAsync().NoContext();

            throw;
        }

        // Runs on its own task so Connect never blocks; classified here because nothing else observes this task.
        var pumping = Task.Run(
            async () => {
                try {
                    await Consume(run, messages, head, position).NoContext();
                } catch (Exception) when (run.Token.IsCancellationRequested) {
                    // Normal shutdown: the token cancelled the read, which is what teardown waits on.
                } catch (Exception e) {
                    run.Fail(DropReason.ServerError, e);
                }
            },
            CancellationToken.None
        );

        // Join the pump first, then the enumerator, then the subscription: the enumerator's generated
        // iterator shares one value-task source with an in-flight read, so disposing it before the pump
        // has stopped reading would re-enter that source and fault on a pool thread.
        run.OnDisconnect(async _ => {
            await pumping.NoContext();
            await messages.DisposeAsync().NoContext();
            await subscription.DisposeAsync().NoContext();
        });

        return;

        FromAll GetPosition() => position switch {
            null when Options.StartFrom == InitialPosition.Latest => FromAll.End,
            null                                                  => FromAll.Start,
            _                                                     => FromAll.After(new(position.Value, position.Value))
        };
    }

    /// <summary>
    /// Consumes the subscription messages, dispatching events and server checkpoints to the same
    /// handlers as before, plus the caught-up notification, which the callback-based client API
    /// silently discards. The message-based API is used precisely to observe that notification.
    /// </summary>
    /// <remarks>
    /// Returns only after reporting a consumer error; every other exit throws, leaving drop-vs-shutdown
    /// classification to the caller.
    /// </remarks>
    async Task Consume(
            SubscriptionRun                 run,
            IAsyncEnumerator<StreamMessage> messages,
            ulong?                          headPosition,
            ulong?                          lastScannedPosition
        ) {
        while (await messages.MoveNextAsync().NoContext()) {
            // Falling behind re-enters catch-up mode: re-read the head as the new commit candidate, since
            // reading it later from the caught-up message could race matches still in flight. Kept outside
            // the inner try because a failure here is a transport failure, not a consumer error.
            if (messages.Current is StreamMessage.FellBehind) {
                headPosition = await GetAllStreamHead(run.Token).NoContext();

                continue;
            }

            try {
                switch (messages.Current) {
                    case StreamMessage.Event(var resolvedEvent):
                        lastScannedPosition = GetContextPosition(resolvedEvent);
                        await HandleInternal(run, CreateContext(run, resolvedEvent, run.Token)).NoContext();

                        break;
                    case StreamMessage.AllStreamCheckpointReached(var checkpointPosition):
                        lastScannedPosition = checkpointPosition.CommitPosition;
                        await HandleCheckpointReached(run, checkpointPosition, run.Token).NoContext();

                        break;
                    case StreamMessage.CaughtUp:
                        // The server reached the live edge, so the commit candidate — the head read
                        // before (re-)entering catch-up mode — has been scanned even though no
                        // checkpoint message reported it. The client's caught-up message carries no
                        // position, so that read is the best provably scanned position available;
                        // skip it once something newer is already known.
                        if (headPosition is { } head && (lastScannedPosition is not { } lastScanned || head > lastScanned)) {
                            lastScannedPosition = head;
                            await HandleCheckpointReached(run, new(head, head), run.Token).NoContext();
                        }

                        break;
                }
            } catch (Exception ex) when (!run.Token.IsCancellationRequested) {
                // Handling a message failed: the transport is fine, the consumer is not — same
                // classification the callback-based API gave to errors thrown by its callbacks.
                // DeserializeData rethrows the raw serializer exception, so matching on exception
                // types here would misattribute malformed payloads to the server.
                run.Fail(DropReason.SubscriptionError, ex);

                return;
            }
        }

        // Server closed the stream; thrown rather than reported, so shutdown-vs-drop is classified by the
        // same filter that covers a read failing mid-shutdown.
        throw new InvalidOperationException($"Subscription {Options.SubscriptionId} to $all: message stream ended unexpectedly");
    }

    async Task<ulong?> GetAllStreamHead(CancellationToken cancellationToken) {
        var lastEvent = await Client
            .ReadAllAsync(Direction.Backwards, Position.End, 1, userCredentials: Options.Credentials, cancellationToken: cancellationToken)
            .ToArrayAsync(cancellationToken)
            .NoContext();

        return lastEvent.Length == 0 ? null : lastEvent[0].Event.Position.CommitPosition;
    }

    /// <summary>
    /// The delivered record's own position in $all — the link's position for a resolved link event,
    /// never the resolved target's. The target can be arbitrarily older than the subscription cursor
    /// (a link created after the caught-up commit can point far behind the committed head), and this
    /// position flows into the checkpoint on ack, so using the target's position would regress the
    /// stored checkpoint.
    /// </summary>
    static ulong GetContextPosition(ResolvedEvent re) => (re.OriginalPosition ?? re.OriginalEvent.Position).CommitPosition;

    MessageConsumeContext CreateContext(SubscriptionRun run, ResolvedEvent re, CancellationToken cancellationToken) {
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
            GetContextPosition(re),
            run.NextSequence(),
            re.Event.Created,
            evt,
            MetadataSerializer.DeserializeMeta(Options, re.Event.Metadata, re.Event.EventStreamId),
            SubscriptionId,
            cancellationToken
        );
    }

    /// <summary>
    /// Handles a position known to be fully scanned by the server — a reported checkpoint, or the
    /// pre-subscribe head on the caught-up transition — by routing it through the same ordered commit
    /// machinery as real events, as a payload-less context. Without this, the stored checkpoint would
    /// only advance when a filter-matched event is processed, so a long unmatched stretch (sparse
    /// filters, quiet servers) leaves the checkpoint parked at the last matched event: restarts re-scan
    /// everything since then, and consumers comparing the checkpoint to the $all head see a phantom,
    /// never-closing lag.
    /// </summary>
    Task HandleCheckpointReached(SubscriptionRun run, global::KurrentDB.Client.Position position, CancellationToken cancellationToken) {
        var context = new MessageConsumeContext(
            position.CommitPosition.ToString(),
            CheckpointReachedMessageType,
            "",
            "$all",
            position.CommitPosition,
            position.CommitPosition,
            position.CommitPosition,
            run.NextSequence(),
            DateTime.UtcNow,
            null,
            null,
            SubscriptionId,
            cancellationToken
        );

        return HandleInternal(run, context).AsTask();
    }

    /// <summary>
    /// Returns a measure delegate for the subscription
    /// </summary>
    /// <returns></returns>
    public GetSubscriptionEndOfStream GetMeasure() => new AllStreamSubscriptionMeasure(Options.SubscriptionId, Client).GetEndOfStream;
}
