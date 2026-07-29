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
            KurrentDBClient             client,
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

    KurrentDBClient.StreamSubscriptionResult? _subscription;
    Task?                                     _messagePump;

    // The highest $all position known to be scanned by the server in the current run: seeded from the
    // stored checkpoint on (re)subscribe, advanced by every received event and checkpoint message. The
    // caught-up commit must never go below it — the commit machinery is gated by sequence, not by
    // position, so an older position submitted later would regress the stored checkpoint.
    ulong? _lastScannedPosition;

    /// <summary>
    /// Starts the subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var filterOptions = new SubscriptionFilterOptions(Options.EventFilter ?? EventTypeFilter.ExcludeSystemEvents(), Options.CheckpointInterval);

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        // The $all head, read before subscribing: by the time the server reports the subscription as
        // caught up, everything at or below this position has provably been scanned, so it can be
        // committed even if no event or checkpoint message ever surfaced it (small stores never cross
        // the checkpoint interval, idle tails park up to one interval below the head).
        var headPosition = await GetAllStreamHead(cancellationToken).NoContext();
        _lastScannedPosition = position;

        var fromAll = GetPosition();

        var subscription = Client.SubscribeToAll(fromAll, Options.ResolveLinkTos, filterOptions, Options.Credentials, cancellationToken);
        var messages     = subscription.Messages.GetAsyncEnumerator(cancellationToken);

        try {
            if (!await messages.MoveNextAsync().NoContext() || messages.Current is not StreamMessage.SubscriptionConfirmation) {
                throw new InvalidOperationException($"Subscription {Options.SubscriptionId} to $all could not be confirmed");
            }
        } catch {
            await messages.DisposeAsync().NoContext();
            subscription.Dispose();

            throw;
        }

        _subscription = subscription;
        _messagePump  = Task.Run(() => PumpMessages(subscription, messages, headPosition, cancellationToken), CancellationToken.None);

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
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    async Task PumpMessages(
            KurrentDBClient.StreamSubscriptionResult subscription,
            IAsyncEnumerator<StreamMessage>          messages,
            ulong?                                   headPosition,
            CancellationToken                        cancellationToken
        ) {
        try {
            while (await messages.MoveNextAsync().NoContext()) {
                // Falling behind re-enters catch-up mode, making the current head the new caught-up
                // commit candidate: every match at or below it is delivered before the next caught-up
                // notification, exactly like the pre-subscribe head on the initial catch-up. Reading the
                // head on the caught-up message instead would be unsafe — matches between the server's
                // live transition and the read could still be in flight, and committing past them skips
                // them on restart. Handled outside the inner try because the read is a server call: its
                // failures are transport failures and must reach the outer catch, not get labelled as
                // consumer errors.
                if (messages.Current is StreamMessage.FellBehind) {
                    headPosition = await GetAllStreamHead(cancellationToken).NoContext();

                    continue;
                }

                try {
                    switch (messages.Current) {
                        case StreamMessage.Event(var resolvedEvent):
                            _lastScannedPosition = GetContextPosition(resolvedEvent);
                            await HandleInternal(CreateContext(resolvedEvent, cancellationToken)).NoContext();

                            break;
                        case StreamMessage.AllStreamCheckpointReached(var checkpointPosition):
                            _lastScannedPosition = checkpointPosition.CommitPosition;
                            await HandleCheckpointReached(checkpointPosition, cancellationToken).NoContext();

                            break;
                        case StreamMessage.CaughtUp:
                            // The server reached the live edge, so the commit candidate — the head read
                            // before (re-)entering catch-up mode — has been scanned even though no
                            // checkpoint message reported it. The client's caught-up message carries no
                            // position, so that read is the best provably scanned position available;
                            // skip it once something newer is already known.
                            if (headPosition is { } head && (_lastScannedPosition is not { } lastScanned || head > lastScanned)) {
                                _lastScannedPosition = head;
                                await HandleCheckpointReached(new(head, head), cancellationToken).NoContext();
                            }

                            break;
                    }
                } catch (Exception ex) when (!cancellationToken.IsCancellationRequested) {
                    // Handling a message failed: the transport is fine, the consumer is not — same
                    // classification the callback-based API gave to errors thrown by its callbacks.
                    // DeserializeData rethrows the raw serializer exception, so matching on exception
                    // types here would misattribute malformed payloads to the server.
                    Dropped(DropReason.SubscriptionError, ex);

                    return;
                }
            }

            // The server ended the message stream without an error and without being asked to stop:
            // treat it as a drop, so the subscription resubscribes instead of staying silently dead
            if (!cancellationToken.IsCancellationRequested) {
                Dropped(DropReason.ServerError, new InvalidOperationException($"Subscription {Options.SubscriptionId} message stream ended unexpectedly"));
            }
        } catch (Exception) when (cancellationToken.IsCancellationRequested) {
            // Normal shutdown: the subscription got disposed or the token got cancelled mid-read
        } catch (Exception ex) {
            Dropped(DropReason.ServerError, ex);
        } finally {
            // Double disposal on the unsubscribe path is fine; on the dropped path this is the only
            // cleanup of the underlying call before Resubscribe replaces the subscription.
            await messages.DisposeAsync().NoContext();
            subscription.Dispose();
        }
    }

    async Task<ulong?> GetAllStreamHead(CancellationToken cancellationToken) {
        var lastEvent = await Client
            .ReadAllAsync(Direction.Backwards, Position.End, 1, userCredentials: Options.Credentials, cancellationToken: cancellationToken)
            .ToArrayAsync(cancellationToken)
            .NoContext();

        return lastEvent.Length == 0 ? null : lastEvent[0].Event.Position.CommitPosition;
    }

    /// <summary>
    /// Stops the subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            Stopping.Cancel(false);
            _subscription?.Dispose();
            _subscription = null;

            if (_messagePump is { } pump) {
                await Task.WhenAny(pump, Task.Delay(100, cancellationToken)).NoContext();
                _messagePump = null;
            }
        } catch (Exception) {
            // Nothing to see here
        }
    }

    /// <summary>
    /// The delivered record's own position in $all — the link's position for a resolved link event,
    /// never the resolved target's. The target can be arbitrarily older than the subscription cursor
    /// (a link created after the caught-up commit can point far behind the committed head), and this
    /// position flows into the checkpoint on ack, so using the target's position would regress the
    /// stored checkpoint.
    /// </summary>
    static ulong GetContextPosition(ResolvedEvent re) => (re.OriginalPosition ?? re.OriginalEvent.Position).CommitPosition;

    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
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
            GetContextPosition(re),
            Sequence++,
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
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    Task HandleCheckpointReached(global::KurrentDB.Client.Position position, CancellationToken cancellationToken) {
        var context = new MessageConsumeContext(
            position.CommitPosition.ToString(),
            CheckpointReachedMessageType,
            "",
            "$all",
            position.CommitPosition,
            position.CommitPosition,
            position.CommitPosition,
            Sequence++,
            DateTime.UtcNow,
            null,
            null,
            SubscriptionId,
            cancellationToken
        );

        return HandleInternal(context).AsTask();
    }

    /// <summary>
    /// Returns a measure delegate for the subscription
    /// </summary>
    /// <returns></returns>
    public GetSubscriptionEndOfStream GetMeasure() => new AllStreamSubscriptionMeasure(Options.SubscriptionId, Client).GetEndOfStream;
}
