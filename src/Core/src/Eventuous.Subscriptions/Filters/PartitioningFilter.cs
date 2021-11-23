using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters.Partitioning;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions.Filters;

public class PartitioningFilter : ConsumeFilter<DelayedAckConsumeContext> {
    readonly int                          _partitionCount;
    readonly Partitioner.GetPartitionHash _partitioner;
    readonly ConcurrentFilter[]           _filters;

    public PartitioningFilter(uint partitionCount, Partitioner.GetPartitionHash? partitioner = null) {
        _partitionCount = (int)partitionCount;
        _partitioner    = partitioner ?? (ctx => MurmurHash3.Hash(ctx.Stream));
        _filters        = Enumerable.Range(0, _partitionCount).Select(_ => new ConcurrentFilter(1)).ToArray();
    }

    public override ValueTask Send(DelayedAckConsumeContext context, Func<DelayedAckConsumeContext, ValueTask>? next) {
        var hash      = _partitioner(context);
        var partition = hash % _partitionCount;
        Log.SendingMessageToPartition(context, partition);
        return _filters[partition].Send(context, next);
    }
}