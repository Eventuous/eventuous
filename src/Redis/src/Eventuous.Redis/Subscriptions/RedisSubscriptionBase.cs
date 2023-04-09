// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;

namespace Eventuous.Redis.Subscriptions;

public abstract class RedisSubscriptionBase<T> : EventSubscriptionWithCheckpoint<T>
    where T : RedisSubscriptionBaseOptions {
    readonly IMetadataSerializer     _metaSerializer;
    readonly CancellationTokenSource _cts = new();

    protected GetRedisDatabase GetDatabase { get; }

    protected RedisSubscriptionBase(
        GetRedisDatabase getDatabase,
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        ILoggerFactory?  loggerFactory
    ) : base(options, checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory) {
        GetDatabase     = Ensure.NotNull(getDatabase, "Connection factory");
        _metaSerializer = DefaultMetadataSerializer.Instance;
    }

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        await BeforeSubscribe(cancellationToken).NoContext();

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _runner = Task.Run(() => PollingQuery(position + 1, _cts.Token), _cts.Token);
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            _cts.Cancel();
            if (_runner != null) await _runner.NoContext();
        }
        catch (OperationCanceledException) {
            // Nothing to do
        }
    }

    protected const string ContentType = "application/json";

    Task? _runner;

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : 0;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                var persistentEvents = await ReadEvents(GetDatabase(), start);

                foreach (var persistentEvent in persistentEvents) {
                    await HandleInternal(ToConsumeContext(persistentEvent, cancellationToken)).NoContext();
                    start = persistentEvent.StreamPosition + 1;
                }
            }
            catch (OperationCanceledException) {
                // Nothing to do
            }
            catch (Exception e) {
                IsDropped = true;
                Log.WarnLog?.Log(e, "Subscription dropped");
                throw;
            }
        }
    }

    IMessageConsumeContext ToConsumeContext(ReceivedEvent evt, CancellationToken cancellationToken) {
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

    IMessageConsumeContext AsContext(ReceivedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => new MessageConsumeContext(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            evt.StreamName,
            (ulong)evt.StreamPosition,
            (ulong)evt.GlobalPosition,
            _sequence++,
            evt.Created,
            e,
            meta,
            Options.SubscriptionId,
            cancellationToken
        );

    protected abstract Task<ReceivedEvent[]> ReadEvents(IDatabase database, long position);

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken)
        => Task.CompletedTask;

    ulong _sequence;
}

public abstract record RedisSubscriptionBaseOptions : SubscriptionOptions {
    public int ConcurrencyLimit { get; set; } = 1;
    public int MaxPageSize      { get; set; } = 100;
}