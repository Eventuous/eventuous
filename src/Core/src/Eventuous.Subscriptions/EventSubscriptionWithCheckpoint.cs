// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

using Checkpoints;
using Context;
using Filters;
using Logging;

public abstract class EventSubscriptionWithCheckpoint<T> : EventSubscription<T> where T : SubscriptionOptions {
    protected EventSubscriptionWithCheckpoint(
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        int              concurrencyLimit,
        ILoggerFactory?  loggerFactory
    )
        : base(Ensure.NotNull(options), ConfigurePipe(consumePipe, concurrencyLimit), loggerFactory) {
        CheckpointStore = Ensure.NotNull(checkpointStore);

        CheckpointCommitHandler = new CheckpointCommitHandler(options.SubscriptionId, checkpointStore, 10, loggerFactory);
    }

    static bool PipelineIsAsync(ConsumePipe pipe) => pipe.RegisteredFilters.Any(x => x is AsyncHandlingFilter);

    // It's not ideal, but for now if there's any filter added on top of the default one,
    // we won't add the concurrent filter, so it won't clash with any custom setup
    static ConsumePipe ConfigurePipe(ConsumePipe pipe, int concurrencyLimit)
        => PipelineIsAsync(pipe) ? pipe : pipe.AddFilterFirst(new AsyncHandlingFilter((uint)concurrencyLimit));

    EventPosition?          LastProcessed           { get; set; }
    CheckpointCommitHandler CheckpointCommitHandler { get; }
    ICheckpointStore        CheckpointStore         { get; }

    protected abstract EventPosition GetPositionFromContext(IMessageConsumeContext context);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected async ValueTask HandleInternal(IMessageConsumeContext context) {
        try {
            Logger.Current = Log;
            var ctx = new AsyncConsumeContext(context, Ack, Nack);
            await Handler(ctx).NoContext();
        } catch (Exception e) {
            context.LogContext.MessageHandlingFailed(Options.SubscriptionId, context, e);

            if (Options.ThrowOnError) throw;
        }
    }

    ValueTask Ack(IMessageConsumeContext context) {
        var eventPosition = GetPositionFromContext(context);
        LastProcessed = eventPosition;

        context.LogContext.TraceLog?.Log("Message {Type} acknowledged at {Position}", context.MessageType, context.GlobalPosition);

        return CheckpointCommitHandler.Commit(
            new CommitPosition(eventPosition.Position!.Value, context.Sequence, eventPosition.Created) { LogContext = context.LogContext },
            context.CancellationToken
        );
    }

    ValueTask Nack(IMessageConsumeContext context, Exception exception) {
        context.LogContext.WarnLog?.Log(exception, "Message {Type} not acknowledged at {Position}", context.MessageType, context.GlobalPosition);

        return Options.ThrowOnError ? throw exception : Ack(context);
    }

    protected async Task<Checkpoint> GetCheckpoint(CancellationToken cancellationToken) {
        if (IsRunning && LastProcessed != null) { return new Checkpoint(Options.SubscriptionId, LastProcessed.Position); }

        Logger.Current = Log;

        var checkpoint = await CheckpointStore.GetLastCheckpoint(Options.SubscriptionId, cancellationToken).NoContext();

        LastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

        return checkpoint;
    }

    protected override ValueTask Finalize(CancellationToken cancellationToken) => CheckpointCommitHandler.DisposeAsync();
}
