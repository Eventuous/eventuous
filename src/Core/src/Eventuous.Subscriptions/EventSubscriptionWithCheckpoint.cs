// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

public abstract class EventSubscriptionWithCheckpoint<T> : EventSubscription<T> where T : SubscriptionOptions {
    protected EventSubscriptionWithCheckpoint(
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        int              concurrencyLimit,
        ILoggerFactory?  loggerFactory
    ) : base(options, ConfigurePipe(consumePipe, concurrencyLimit), loggerFactory) {
        CheckpointStore = Ensure.NotNull(checkpointStore);

        CheckpointCommitHandler = new CheckpointCommitHandler(
            options.SubscriptionId,
            checkpointStore,
            10,
            loggerFactory
        );
    }

    static bool PipelineIsAsync(ConsumePipe pipe) => pipe.RegisteredFilters.Any(x => x is AsyncHandlingFilter);

    // It's not ideal, but for now if there's any filter added on top of the default one,
    // we won't add the concurrent filter, so it won't clash with any custom setup
    static ConsumePipe ConfigurePipe(ConsumePipe pipe, int concurrencyLimit)
        => PipelineIsAsync(pipe)
            ? pipe
            : pipe.AddFilterFirst(new AsyncHandlingFilter((uint)concurrencyLimit));

    protected EventPosition?          LastProcessed           { get; set; }
    protected CheckpointCommitHandler CheckpointCommitHandler { get; }
    protected ICheckpointStore        CheckpointStore         { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected async ValueTask HandleInternal(IMessageConsumeContext context) {
        try {
            Logger.Current = Log;
            var ctx = new AsyncConsumeContext(context, Ack, Nack);
            await Handler(ctx).NoContext();
        }
        catch (Exception e) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);
            if (Options.ThrowOnError) throw;
        }
    }

    ValueTask Ack(IMessageConsumeContext context) {
        var eventPosition = EventPosition.FromContext(context);
        LastProcessed = eventPosition;

        return CheckpointCommitHandler.Commit(
            new CommitPosition(eventPosition.Position!.Value, context.Sequence) { LogContext = context.LogContext },
            context.CancellationToken
        );
    }

    ValueTask Nack(IMessageConsumeContext context, Exception exception)
        => Options.ThrowOnError ? throw exception : Ack(context);

    protected async Task<Checkpoint> GetCheckpoint(CancellationToken cancellationToken) {
        if (IsRunning && LastProcessed != null) {
            return new Checkpoint(Options.SubscriptionId, LastProcessed.Position);
        }

        Logger.Current = Log;

        var checkpoint = await CheckpointStore
            .GetLastCheckpoint(Options.SubscriptionId, cancellationToken)
            .NoContext();

        LastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

        return checkpoint;
    }

    protected async Task StoreCheckpoint(EventPosition eventPosition, CancellationToken cancellationToken) {
        LastProcessed = eventPosition;

        Logger.Current = Log;

        await CheckpointStore.StoreCheckpoint(
            new Checkpoint(SubscriptionId, eventPosition.Position),
            true,
            cancellationToken
        );
    }

    protected override ValueTask Finalize(CancellationToken cancellationToken)
        => CheckpointCommitHandler.DisposeAsync();
}
