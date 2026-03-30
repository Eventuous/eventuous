// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

using System.Diagnostics.CodeAnalysis;
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

    EventPosition?           LastProcessed           { get; set; }
    CheckpointCommitHandler? CheckpointCommitHandler { get; set; }

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

    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    protected async ValueTask HandleInternal(IMessageConsumeContext context) {
        try {
            Logger.Current = Log;
            var ctx = new AsyncConsumeContext(context, Ack, NackOnAsyncWorker);
            await Handler(ctx).NoContext();
        } catch (OperationCanceledException e) when (context.CancellationToken.IsCancellationRequested) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);
            Dropped(DropReason.Stopped, e);
        } catch (Exception e) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);

            if (Options.ThrowOnError) throw;
        }
    }

    /// <summary>
    /// Wraps the Nack callback for the async worker path. When ThrowOnError is true,
    /// Nack throws to signal a fatal error. On the async worker thread (AsyncHandlingFilter),
    /// that throw would silently kill the channel worker without triggering Dropped/Resubscribe.
    /// This wrapper catches the throw and calls Dropped instead.
    /// </summary>
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    ValueTask NackOnAsyncWorker(IMessageConsumeContext context, Exception exception) {
        try {
            return Nack(context, exception);
        } catch (Exception) {
            Dropped(DropReason.SubscriptionError, exception);

            return default;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ValueTask Ack(IMessageConsumeContext context) {
        // Capture locally — CheckpointCommitHandler can be nulled by Resubscribe/DisposeCommitHandler
        // on another thread while the async worker is still completing a message.
        var handler = CheckpointCommitHandler;

        if (handler is null) return default;

        var eventPosition = GetPositionFromContext(context);
        LastProcessed = eventPosition;

        context.LogContext.MessageAcked(context.MessageType, context.GlobalPosition);

        return handler.Commit(
            new(eventPosition.Position!.Value, context.Sequence, eventPosition.Created) { LogContext = context.LogContext },
            context.CancellationToken
        );
    }

    ValueTask Nack(IMessageConsumeContext context, Exception exception) {
        context.LogContext.MessageNacked(context.MessageType, context.GlobalPosition, exception);

        return Options.ThrowOnError ? throw exception : Ack(context);
    }

    protected async Task<Checkpoint> GetCheckpoint(CancellationToken cancellationToken) {
        CheckpointCommitHandler ??= new(
            options.SubscriptionId,
            checkpointStore,
            TimeSpan.FromMilliseconds(options.CheckpointCommitDelayMs),
            options.CheckpointCommitBatchSize,
            LoggerFactory
        );

        if (IsRunning && LastProcessed != null) { return new(Options.SubscriptionId, LastProcessed?.Position); }

        Logger.Current = Log;

        var checkpoint = await CheckpointStore.GetLastCheckpoint(Options.SubscriptionId, cancellationToken).NoContext();

        LastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

        return checkpoint;
    }

    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    protected override async Task Resubscribe(TimeSpan delay, CancellationToken cancellationToken) {
        // Reset checkpoint state so the new run reads from the committed checkpoint,
        // not from LastProcessed (which may be ahead of the failed event).
        LastProcessed = null;
        Sequence = 0;

        await DisposeCommitHandler();

        await base.Resubscribe(delay, cancellationToken);
    }

    protected override async ValueTask Finalize(CancellationToken cancellationToken) => await DisposeCommitHandler();

    async ValueTask DisposeCommitHandler() {
        // Swap to null first so the concurrent path (Resubscribe vs Finalize) sees null.
        var handler = CheckpointCommitHandler;
        CheckpointCommitHandler = null;

        if (handler != null) await handler.DisposeAsync();
    }
}
