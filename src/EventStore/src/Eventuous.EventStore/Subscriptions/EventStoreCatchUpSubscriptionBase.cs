using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract class EventStoreCatchUpSubscriptionBase<T> : EventStoreSubscriptionBase<T>
    where T : EventStoreSubscriptionOptions {
    protected ICheckpointStore CheckpointStore { get; }

    protected EventStoreCatchUpSubscriptionBase(
        EventStoreClient eventStoreClient,
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        ILoggerFactory?  loggerFactory = null
    ) : base(
        eventStoreClient,
        options,
        ConfigurePipe(consumePipe),
        loggerFactory
    ) {
        CheckpointStore = Ensure.NotNull(checkpointStore, nameof(checkpointStore));
    }

    static ConsumePipe ConfigurePipe(ConsumePipe pipe)
        => pipe
            .AddFilterFirst(new MessageFilter(ctx => !ctx.MessageType.StartsWith("$")))
            .AddFilterFirst(new ConcurrentFilter(1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected ValueTask HandleInternal(IMessageConsumeContext context) {
        var ctx = new DelayedAckConsumeContext(Ack, context);
        return Handler(ctx);

        ValueTask Ack(CancellationToken ct) {
            var position = EventPosition.FromContext(context);
            return StoreCheckpoint(position, ct);
        }
    }

    protected async Task<Checkpoint> GetCheckpoint(CancellationToken cancellationToken) {
        if (IsRunning && LastProcessed != null) {
            return new Checkpoint(Options.SubscriptionId, LastProcessed.Position);
        }

        var checkpoint = await CheckpointStore
            .GetLastCheckpoint(Options.SubscriptionId, cancellationToken)
            .NoContext();

        LastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

        return checkpoint;
    }

    protected async ValueTask StoreCheckpoint(
        EventPosition     position,
        CancellationToken cancellationToken
    ) {
        var checkpoint = new Checkpoint(Options.SubscriptionId, position.Position);
        await CheckpointStore.StoreCheckpoint(checkpoint, cancellationToken).NoContext();
        LastProcessed = position;
    }

    protected override ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            Subscription?.Dispose();
        }
        catch (Exception e) {
            Log.LogInformation(
                "Subscription {SubscriptionId} stopped: {Message}",
                SubscriptionId,
                e.Message
            );
        }

        return default;
    }

    protected global::EventStore.Client.StreamSubscription? Subscription { get; set; }
}