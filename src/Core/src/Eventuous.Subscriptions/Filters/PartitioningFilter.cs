using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters.Partitioning;
using static Eventuous.Subscriptions.Filters.Partitioning.Partitioner;

namespace Eventuous.Subscriptions.Filters;

public sealed class PartitioningFilter : ConsumeFilter<DelayedAckConsumeContext>, IAsyncDisposable {
    readonly GetPartitionHash   _getHash;
    readonly GetPartitionKey    _partitioner;
    readonly ConcurrentFilter[] _filters;
    readonly int                _partitionCount;

    public PartitioningFilter(
        int               partitionCount,
        GetPartitionKey?  partitioner   = null,
        GetPartitionHash? getHash       = null
    ) {
        if (partitionCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(partitionCount), "Partition count must be greater than zero");

        _getHash        = getHash ?? MurmurHash3.Hash;
        _partitionCount = partitionCount;
        _partitioner    = partitioner ?? (ctx => ctx.Stream);

        _filters = Enumerable.Range(0, _partitionCount)
            .Select(_ => new ConcurrentFilter(1))
            .ToArray();
    }

    protected override ValueTask Send(DelayedAckConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
        var partitionKey = _partitioner(context);
        var hash         = _getHash(partitionKey);
        var partition    = hash % _partitionCount;
        context.PartitionKey = partitionKey;
        context.PartitionId  = partition;
        return _filters[partition].Send(context, next);
    }

    public async ValueTask DisposeAsync() {
        // Logger.Current.Info("Partitioner is stopping concurrent filters");
        await Task.WhenAll(_filters.Select(async x => await x.DisposeAsync()));
    }
}
