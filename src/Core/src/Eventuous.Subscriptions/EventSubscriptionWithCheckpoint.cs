// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Tools;

namespace Eventuous.Subscriptions;

public abstract class EventSubscriptionWithCheckpoint<T> : EventSubscription<T> where T : SubscriptionOptions {
    protected EventSubscriptionWithCheckpoint(
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        int              concurrencyLimit
    ) : base(options, ConfigurePipe(consumePipe, concurrencyLimit)) {
        CheckpointStore         = Ensure.NotNull(checkpointStore);
        CheckpointCommitHandler = new CheckpointCommitHandler(options.SubscriptionId, checkpointStore, 10);
    }

    // It's not ideal, but for now if there's any filter added on top of the default one,
    // we won't add the concurrent filter, so it won't clash with any custom setup
    static ConsumePipe ConfigurePipe(ConsumePipe pipe, int concurrencyLimit)
        => pipe.RegisteredFilters.All(x => x is not ConcurrentFilter)
            ? pipe.AddFilterFirst(new PartitioningFilter(concurrencyLimit))
            : pipe;

    protected EventPosition?          LastProcessed           { get; set; }
    protected CheckpointCommitHandler CheckpointCommitHandler { get; }
    protected ICheckpointStore        CheckpointStore         { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected async ValueTask HandleInternal(IMessageConsumeContext context) {
        try {
            var ctx = new DelayedAckConsumeContext(context, Ack, Nack);
            await Handler(ctx).NoContext();
        }
        catch (Exception e) {
            SubscriptionsEventSource.Log.MessageHandlingFailed(Options.SubscriptionId, context, e);
            if (Options.ThrowOnError) throw;
        }
    }

    ValueTask Ack(IMessageConsumeContext ctx) {
        var eventPosition = EventPosition.FromContext(ctx);
        LastProcessed = eventPosition;

        return CheckpointCommitHandler.Commit(
            new CommitPosition(eventPosition.Position!.Value, ctx.Sequence),
            ctx.CancellationToken
        );
    }

    ValueTask Nack(IMessageConsumeContext ctx, Exception exception)
        => Options.ThrowOnError ? throw exception : Ack(ctx);

    protected async Task<Checkpoint> GetCheckpoint(CancellationToken cancellationToken) {
        if (IsRunning && LastProcessed != null) {
            return new Checkpoint(Options.SubscriptionId, LastProcessed.Position);
        }

        var checkpoint = await CheckpointStore
            .GetLastCheckpoint(Options.SubscriptionId, cancellationToken)
            .NoContext();

        LastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

        return checkpoint;
    }

    protected async Task StoreCheckpoint(EventPosition eventPosition, CancellationToken cancellationToken) {
        LastProcessed = eventPosition;

        await CheckpointStore.StoreCheckpoint(
            new Checkpoint(SubscriptionId, eventPosition.Position),
            true,
            cancellationToken
        );
    }
    
    protected override ValueTask Finalize(CancellationToken cancellationToken)
        => CheckpointCommitHandler.DisposeAsync();
}
