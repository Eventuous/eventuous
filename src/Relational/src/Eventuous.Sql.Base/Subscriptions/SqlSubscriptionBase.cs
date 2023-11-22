// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sql.Base.Subscriptions;

public abstract class SqlSubscriptionBase<T>(
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        int              concurrencyLimit,
        ILoggerFactory?  loggerFactory
    )
    : EventSubscriptionWithCheckpoint<T>(options, checkpointStore, consumePipe, concurrencyLimit, loggerFactory) where T : SqlSubscriptionOptionsBase {
    readonly IMetadataSerializer     _metaSerializer = DefaultMetadataSerializer.Instance;
    readonly CancellationTokenSource _cts            = new();

    protected abstract Task PollingQuery(ulong? position, CancellationToken cancellationToken);

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        await BeforeSubscribe(cancellationToken).NoContext();

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _runner = Task.Run(() => PollingQuery(position, _cts.Token), _cts.Token);
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            _cts.Cancel();
            if (_runner != null) await _runner.NoContext();
        } catch (OperationCanceledException) {
            // Nothing to do
        } catch (InvalidOperationException e) when (e.Message.Contains("Operation cancelled by user.")) {
            // It's a wrapped task cancelled exception
        }
    }

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected abstract long MoveStart(PersistedEvent evt);

    protected IMessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(ContentType, evt.MessageType, Encoding.UTF8.GetBytes(evt.JsonData), evt.StreamName!, (ulong)evt.StreamPosition);

        var meta = evt.JsonMetadata == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(evt, data, meta, cancellationToken);
    }

    protected abstract IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken);

    Task? _runner;

    protected const string ContentType = "application/json";
}
