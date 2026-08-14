// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

using Checkpoints;
using Context;
using Filters;
using Logging;

public enum SubscriptionKind {
    Stream,
    All
}

public abstract class EventSubscriptionWithCheckpoint<T>(
        T                    options,
        ICheckpointStore     checkpointStore,
        ConsumePipe          consumePipe,
        int                  concurrencyLimit,
        SubscriptionKind     kind,
        ILoggerFactory?      loggerFactory,
        IEventSerializer?    eventSerializer,
        IMetadataSerializer? metadataSerializer
    )
    : EventSubscription<T>(Ensure.NotNull(options), ConfigurePipe(consumePipe, concurrencyLimit), loggerFactory, eventSerializer)
    where T : SubscriptionWithCheckpointOptions {
    static bool PipelineIsAsync(ConsumePipe pipe) => pipe.RegisteredFilters.Any(x => x is AsyncHandlingFilter);

    // It's not ideal, but for now if there's any filter added on top of the default one,
    // we won't add the concurrent filter, so it won't clash with any custom setup
    static ConsumePipe ConfigurePipe(ConsumePipe pipe, int concurrencyLimit)
        => PipelineIsAsync(pipe) ? pipe : pipe.AddFilterFirst(new AsyncHandlingFilter((uint)concurrencyLimit));

    protected ICheckpointStore CheckpointStore { get; } = Ensure.NotNull(checkpointStore);

    protected SubscriptionKind Kind { get; } = kind;

    protected IMetadataSerializer MetadataSerializer { get; } = metadataSerializer ?? DefaultMetadataSerializer.Instance;

    EventPosition GetPositionFromContext(IMessageConsumeContext context)
#pragma warning disable CS8524
        => Kind switch {
#pragma warning restore CS8524
            SubscriptionKind.All    => EventPosition.FromAllContext(context),
            SubscriptionKind.Stream => EventPosition.FromContext(context)
        };

    /// <summary>
    /// A run carrying this attempt's own commit handler, so an acknowledgement reaches the handler that
    /// dispatched it, and the base class never has to know checkpoints exist.
    /// </summary>
    sealed class CheckpointedRun(CancellationToken lifetime, CheckpointCommitHandler checkpoint) : SubscriptionRun(lifetime) {
        internal CheckpointCommitHandler Checkpoint { get; } = checkpoint;
    }

    /// <summary>
    /// Sealed so every run reaching this class carries a commit handler, letting <see cref="Ack"/> find one
    /// without a lookup or a null check.
    /// </summary>
    protected sealed override SubscriptionRun CreateRun(CancellationToken lifetime) {
        var run = new CheckpointedRun(
            lifetime,
            new(
                Options.SubscriptionId,
                CheckpointStore,
                TimeSpan.FromMilliseconds(Options.CheckpointCommitDelayMs),
                Options.CheckpointCommitBatchSize,
                LoggerFactory
            )
        );

        // Registered first, before Connect, so it's the first OnDisconnect registration — and since release
        // order reverses registration order, it releases LAST, after every transport handle. That's
        // checkpoint durability: acks in flight must land before the handler that commits them stops.
        // Moving this into or after Connect would release the handler too early.
        run.OnDisconnect(_ => run.Checkpoint.DisposeAsync());

        return run;
    }

    /// <summary>
    /// Cast holds by construction: every run reaching this class comes from the sealed <see cref="CreateRun"/>.
    /// </summary>
    static CheckpointCommitHandler Checkpoint(SubscriptionRun run) => ((CheckpointedRun)run).Checkpoint;

    /// <summary>
    /// Run is passed explicitly, not looked up, so a message that completes after its run ended acknowledges
    /// into that (refusing) run, not into whichever replaced it.
    /// </summary>
    protected async ValueTask HandleInternal(SubscriptionRun run, IMessageConsumeContext context) {
        try {
            Logger.Current = Log;

            var ctx = new AsyncConsumeContext(context, c => Ack(run, c), (c, e) => NackOnAsyncWorker(run, c, e));
            await Handler(ctx).NoContext();
        } catch (OperationCanceledException e) when (context.CancellationToken.IsCancellationRequested) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);
        } catch (Exception e) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);

            if (Options.ThrowOnError) throw;
        }
    }

    /// <summary>
    /// Nack throws under <c>ThrowOnError</c>; on the <see cref="AsyncHandlingFilter"/> channel worker that
    /// would silently kill the reader, so it's turned into this run's failure instead.
    /// </summary>
    ValueTask NackOnAsyncWorker(SubscriptionRun run, IMessageConsumeContext context, Exception exception) {
        try {
            return Nack(run, context, exception);
        } catch (Exception) {
            run.Fail(DropReason.SubscriptionError, exception);

            return default;
        }
    }

    async ValueTask Ack(SubscriptionRun run, IMessageConsumeContext context) {
        var position = GetPositionFromContext(context);

        // Committed through the dispatching run, never whichever run is current: another run's counter
        // could collide with a live sequence or let the checkpoint advance over a message it never handled.
        //
        // Uncancellable on purpose: a dropped CommitPosition is poison — the handler won't commit past the
        // gap it leaves.
        var commit = new CommitPosition(position.Position!.Value, context.Sequence, position.Created) { LogContext = context.LogContext };

        if (!await Checkpoint(run).Commit(commit, CancellationToken.None).NoContext()) {
            context.LogContext.MessageFromPreviousRunIgnored(context);

            return;
        }

        context.LogContext.MessageAcked(context.MessageType, context.GlobalPosition);
    }

    ValueTask Nack(SubscriptionRun run, IMessageConsumeContext context, Exception exception) {
        context.LogContext.MessageNacked(context.MessageType, context.GlobalPosition, exception);

        return Options.ThrowOnError ? throw exception : Ack(run, context);
    }

    /// <summary>
    /// Called by <c>Connect</c> once per run, before any dispatch. Always reads the store — correct, since
    /// the run being replaced flushed before it ended.
    /// </summary>
    protected async Task<Checkpoint> GetCheckpoint(SubscriptionRun run) {
        Logger.Current = Log;

        return await CheckpointStore.GetLastCheckpoint(Options.SubscriptionId, run.Token).NoContext();
    }
}
