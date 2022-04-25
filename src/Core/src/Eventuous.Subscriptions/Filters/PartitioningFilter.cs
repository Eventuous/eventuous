using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters.Partitioning;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions.Filters;

public sealed class PartitioningFilter : ConsumeFilter<DelayedAckConsumeContext>, IAsyncDisposable {
    readonly Partitioner.GetPartitionHash _getHash;
    readonly Partitioner.GetPartitionKey  _partitioner;
    readonly ConcurrentFilter[]           _filters;
    readonly int                          _partitionCount;

    public PartitioningFilter(
        uint                          partitionCount,
        Partitioner.GetPartitionKey?  partitioner = null,
        Partitioner.GetPartitionHash? getHash     = null
    ) {
        _getHash        = getHash ?? MurmurHash3.Hash;
        _partitionCount = (int)partitionCount;
        _partitioner    = partitioner ?? (ctx => ctx.Stream);
        _filters        = Enumerable.Range(0, _partitionCount).Select(_ => new ConcurrentFilter(1)).ToArray();
    }

    public override ValueTask Send(DelayedAckConsumeContext context, Func<DelayedAckConsumeContext, ValueTask>? next) {
        var partitionKey = _partitioner(context);
        var hash         = _getHash(partitionKey);
        var partition    = hash % _partitionCount;
        context.PartitionKey = partitionKey;
        context.PartitionId  = partition;
        return _filters[partition].Send(context, next);
    }

    public async ValueTask DisposeAsync() {
        Log.Stopping(nameof(PartitioningFilter), "concurrent filters", "");
        await Task.WhenAll(_filters.Select(async x => await x.DisposeAsync()));
    }
}
