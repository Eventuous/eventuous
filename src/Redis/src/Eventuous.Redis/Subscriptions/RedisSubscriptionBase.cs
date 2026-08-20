// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Redis.Subscriptions;

public abstract class RedisSubscriptionBase<T>(
        GetRedisDatabase     getDatabase,
        T                    options,
        ICheckpointStore     checkpointStore,
        ConsumePipe          consumePipe,
        SubscriptionKind     kind,
        ILoggerFactory?      loggerFactory      = null,
        IEventSerializer?    eventSerializer    = null,
        IMetadataSerializer? metadataSerializer = null
    )
    : EventSubscriptionWithCheckpoint<T>(
        options,
        checkpointStore,
        consumePipe,
        options.ConcurrencyLimit,
        kind,
        loggerFactory,
        eventSerializer,
        metadataSerializer
    )
    where T : RedisSubscriptionBaseOptions {
    readonly IMetadataSerializer _metaSerializer = DefaultMetadataSerializer.Instance;

    protected GetRedisDatabase GetDatabase { get; } = Ensure.NotNull(getDatabase, "Connection factory");

    protected override async ValueTask Connect(SubscriptionRun run) {
        await BeforeSubscribe(run.Token).NoContext();

        var checkpoint = await GetCheckpoint(run).NoContext();

        // Resolved before the pump starts: an unsupported StartFrom is a config error and must throw from
        // Connect, not surface as a drop the supervisor retries forever from the pump.
        // Local rather than a field so a later Connect can't move it under a loop still winding down.
        var start = checkpoint.Position is { } position
            ? (long)(position + 1)
            : Options.StartFrom == InitialPosition.Earliest
                ? 0
                : throw new NotSupportedException("Redis subscription does not support latest position");

        // Runs on its own task so Connect never blocks; classified here because nothing else observes this task.
        var pumping = Task.Run(
            async () => {
                try {
                    await Poll(run, start, run.Token).NoContext();
                } catch (Exception) when (run.Token.IsCancellationRequested) {
                    // This run's own token asked for it: graceful, not a drop.
                } catch (Exception e) {
                    // Any other cancellation (an inner deadline, a WaitAsync timeout) is a real drop cause.
                    run.Fail(DropReason.ServerError, e);
                }
            },
            CancellationToken.None
        );

        // No handle of its own to release: registered purely to join the loop before the next Connect starts.
        run.OnDisconnect(_ => new(pumping));
    }

    const string ContentType = "application/json";

    /// <summary>
    /// The polling loop. Its only clean exit is the token; every other exit is a fault the pump in
    /// <see cref="Connect"/> reads as a drop.
    /// </summary>
    async Task Poll(SubscriptionRun run, long start, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                var persistentEvents = await ReadEvents(GetDatabase(), start).NoContext();

                foreach (var persistentEvent in persistentEvents) {
                    await HandleInternal(run, ToConsumeContext(run, persistentEvent, cancellationToken)).NoContext();
                    start = persistentEvent.StreamPosition + 1;
                }
            } catch (InvalidOperationException e) when (e.Message.Contains("Reading is not allowed after reader was completed") ||
                                                        cancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException("Redis read operation terminated", e, cancellationToken);
            }
        }
    }

    MessageConsumeContext ToConsumeContext(SubscriptionRun run, ReceivedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(
            ContentType,
            evt.MessageType,
            Encoding.UTF8.GetBytes(evt.JsonData),
            evt.StreamName,
            (ulong)evt.StreamPosition
        );

        var meta = (evt.JsonMetadata == null) ? new() : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata));

        return AsContext(run, evt, data, meta, cancellationToken);
    }

    MessageConsumeContext AsContext(SubscriptionRun run, ReceivedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => new(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            evt.StreamName,
            (ulong)evt.StreamPosition,
            (ulong)evt.StreamPosition,
            (ulong)evt.GlobalPosition,
            run.NextSequence(),
            evt.Created,
            e,
            meta,
            Options.SubscriptionId,
            cancellationToken
        );

    protected abstract Task<ReceivedEvent[]> ReadEvents(IDatabase database, long position);

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken) => Task.CompletedTask;
}

public abstract record RedisSubscriptionBaseOptions : SubscriptionWithCheckpointOptions {
    public int ConcurrencyLimit { get; set; } = 1;
    public int MaxPageSize      { get; set; } = 100;
}
