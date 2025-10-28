// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;

namespace Eventuous.Subscriptions.Checkpoints;

public class MeasuredCheckpointStore(ICheckpointStore checkpointStore) : ICheckpointStore {
    public const string OperationPrefix    = "checkpoint";
    public const string ReadOperationName  = $"{OperationPrefix}.read";
    public const string WriteOperationName = $"{OperationPrefix}.write";
    public const string SubscriptionIdTag  = "subscriptionId";
    public const string CheckpointBaggage  = "checkpoint";

    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CheckpointInitialPosition _, CancellationToken cancellationToken) {
        using var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
                ReadOperationName,
                ActivityKind.Internal,
                parentContext: default,
                GetTags(checkpointId),
                idFormat: ActivityIdFormat.W3C
            )
            ?.Start();

        var checkpoint = await checkpointStore.GetLastCheckpoint(checkpointId, CheckpointInitialPosition.Beginning, cancellationToken).NoContext();

        activity?.AddBaggage(CheckpointBaggage, checkpoint.Position?.ToString());

        return checkpoint;
    }

    public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        using var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
                WriteOperationName,
                ActivityKind.Internal,
                parentContext: default,
                GetTags(checkpoint.Id),
                idFormat: ActivityIdFormat.W3C
            )
            ?
            .AddBaggage(CheckpointBaggage, checkpoint.Position?.ToString())
            .Start();

        return await checkpointStore.StoreCheckpoint(checkpoint, force, cancellationToken).NoContext();
    }

    static KeyValuePair<string, object?>[] GetTags(string checkpointId)
        => EventuousDiagnostics.CombineWithDefaultTags(new KeyValuePair<string, object?>(SubscriptionIdTag, checkpointId));
}
