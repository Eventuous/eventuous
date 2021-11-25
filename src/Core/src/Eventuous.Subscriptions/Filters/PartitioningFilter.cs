using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters.Partitioning;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions.Filters;

public class PartitioningFilter : ConsumeFilter<DelayedAckConsumeContext>, IAsyncDisposable {
    readonly int                          _partitionCount;
    readonly Partitioner.GetPartitionHash _partitioner;
    readonly ConcurrentFilter[]           _filters;

    public PartitioningFilter(uint partitionCount, Partitioner.GetPartitionHash? partitioner = null) {
        _partitionCount = (int)partitionCount;
        _partitioner    = partitioner ?? (ctx => MurmurHash3.Hash(ctx.Stream));
        _filters        = Enumerable.Range(0, _partitionCount).Select(_ => new ConcurrentFilter(1)).ToArray();
    }

    public PartitioningFilter(uint partitionCount, Partitioner.GetPartitionKey getPartitionKey)
        : this(partitionCount, ctx => MurmurHash3.Hash(getPartitionKey(ctx))) { }

    public override ValueTask Send(DelayedAckConsumeContext context, Func<DelayedAckConsumeContext, ValueTask>? next) {
        var hash      = _partitioner(context);
        var partition = hash % _partitionCount;
        Log.SendingMessageToPartition(context, partition);
        return _filters[partition].Send(context, next);
    }

    public async ValueTask DisposeAsync() {
        Log.Stopping(nameof(PartitioningFilter), "concurrent filters", "");
        await Task.WhenAll(_filters.Select(async x => await x.DisposeAsync()));
    }
}