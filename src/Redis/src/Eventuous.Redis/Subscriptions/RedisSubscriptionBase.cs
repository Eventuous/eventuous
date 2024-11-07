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

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        await BeforeSubscribe(cancellationToken).NoContext();

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _runner = new TaskRunner(token => PollingQuery(position + 1, token)).Start();
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        if (_runner == null) return;

        await _runner.Stop(cancellationToken);
        _runner.Dispose();
        _runner = null;
    }

    const string ContentType = "application/json";

    TaskRunner? _runner;

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : 0;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                var persistentEvents = await ReadEvents(GetDatabase(), start).NoContext();

                foreach (var persistentEvent in persistentEvents) {
                    await HandleInternal(ToConsumeContext(persistentEvent, cancellationToken)).NoContext();
                    start = persistentEvent.StreamPosition + 1;
                }
            } catch (InvalidOperationException e) when (e.Message.Contains("Reading is not allowed after reader was completed") ||
                                                        cancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException("Redis read operation terminated", e, cancellationToken);
            } catch (Exception e) {
                IsDropped = true;
                Log.WarnLog?.Log(e, "Subscription dropped");

                throw;
            }
        }
    }

    MessageConsumeContext ToConsumeContext(ReceivedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(
            ContentType,
            evt.MessageType,
            Encoding.UTF8.GetBytes(evt.JsonData),
            evt.StreamName,
            (ulong)evt.StreamPosition
        );

        var meta = (evt.JsonMetadata == null)
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata));

        return AsContext(evt, data, meta, cancellationToken);
    }

    MessageConsumeContext AsContext(ReceivedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => new(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            evt.StreamName,
            (ulong)evt.StreamPosition,
            (ulong)evt.StreamPosition,
            (ulong)evt.GlobalPosition,
            Sequence++,
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
