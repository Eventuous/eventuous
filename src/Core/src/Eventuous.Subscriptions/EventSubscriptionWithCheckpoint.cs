// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
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

    EventPosition?           LastProcessed           { get; set; }
    CheckpointCommitHandler? CheckpointCommitHandler { get; set; }
    ICheckpointStore         CheckpointStore         { get; } = Ensure.NotNull(checkpointStore);

    protected SubscriptionKind Kind { get; } = kind;
    
    protected IMetadataSerializer MetadataSerializer { get; } = metadataSerializer ?? DefaultMetadataSerializer.Instance;

    EventPosition GetPositionFromContext(IMessageConsumeContext context)
#pragma warning disable CS8524
        => Kind switch {
#pragma warning restore CS8524
            SubscriptionKind.All    => EventPosition.FromAllContext(context),
            SubscriptionKind.Stream => EventPosition.FromContext(context)
        };

    protected async ValueTask HandleInternal(IMessageConsumeContext context) {
        try {
            Logger.Current = Log;
            var ctx = new AsyncConsumeContext(context, Ack, Nack);
            await Handler(ctx).NoContext();
        } catch (OperationCanceledException e) when (context.CancellationToken.IsCancellationRequested) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);
            Dropped(DropReason.Stopped, e);
        } catch (Exception e) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);

            if (Options.ThrowOnError) throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ValueTask Ack(IMessageConsumeContext context) {
        var eventPosition = GetPositionFromContext(context);
        LastProcessed = eventPosition;

        context.LogContext.MessageAcked(context.MessageType, context.GlobalPosition);

        return CheckpointCommitHandler!.Commit(
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

        if (IsRunning && LastProcessed != null) { return new Checkpoint(Options.SubscriptionId, LastProcessed?.Position); }

        Logger.Current = Log;

        var checkpoint = await CheckpointStore.GetLastCheckpoint(Options.SubscriptionId, cancellationToken).NoContext();

        LastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

        return checkpoint;
    }

    protected override async ValueTask Finalize(CancellationToken cancellationToken) {
        if (CheckpointCommitHandler == null) return;

        await CheckpointCommitHandler.DisposeAsync();
        CheckpointCommitHandler = null;
    }
}
