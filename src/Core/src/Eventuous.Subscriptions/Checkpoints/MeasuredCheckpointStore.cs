using System.Diagnostics;
using System.Runtime.CompilerServices;
using Eventuous.Diagnostics;

namespace Eventuous.Subscriptions.Checkpoints;

public class MeasuredCheckpointStore : ICheckpointStore {
    public const string OperationPrefix    = "checkpoint";
    public const string ReadOperationName  = $"{OperationPrefix}.read";
    public const string WriteOperationName = $"{OperationPrefix}.write";
    public const string SubscriptionIdTag  = "subscriptionId";
    public const string CheckpointBaggage  = "checkpoint";

    readonly ICheckpointStore _checkpointStore;

    public MeasuredCheckpointStore(ICheckpointStore checkpointStore)
        => _checkpointStore = checkpointStore;

    public async ValueTask<Checkpoint> GetLastCheckpoint(
        string            checkpointId,
        CancellationToken cancellationToken
    ) {
        using var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            ReadOperationName,
            ActivityKind.Internal,
            parentContext: default,
            new[] { new KeyValuePair<string, object?>(SubscriptionIdTag, checkpointId) },
            idFormat: ActivityIdFormat.W3C
        )?.Start();

        var checkpoint = await _checkpointStore.GetLastCheckpoint(checkpointId, cancellationToken).NoContext();

        activity?.AddBaggage(CheckpointBaggage, checkpoint.Position?.ToString());
        return checkpoint;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        CancellationToken cancellationToken
    ) {
        using var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
                WriteOperationName,
                ActivityKind.Internal,
                parentContext: default,
                new[] { new KeyValuePair<string, object?>(SubscriptionIdTag, checkpoint.Id) },
                idFormat: ActivityIdFormat.W3C
            )?
            .AddBaggage(CheckpointBaggage, checkpoint.Position?.ToString())
            .Start();

        return await _checkpointStore.StoreCheckpoint(checkpoint, cancellationToken).NoContext();
    }
}