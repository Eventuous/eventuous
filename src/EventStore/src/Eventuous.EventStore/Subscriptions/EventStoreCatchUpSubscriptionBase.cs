using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;

namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract class EventStoreCatchUpSubscriptionBase<T> : EventStoreSubscriptionBase<T>
    where T : EventStoreSubscriptionOptions {
    protected ICheckpointStore CheckpointStore { get; }

    protected EventStoreCatchUpSubscriptionBase(
        EventStoreClient eventStoreClient,
        T                options,
        ICheckpointStore checkpointStore,
        IMessageConsumer consumer,
        ILoggerFactory?  loggerFactory = null
    ) : base(
        eventStoreClient,
        options,
        GetConsumer(consumer),
        loggerFactory
    ) {
        CheckpointStore = Ensure.NotNull(checkpointStore, nameof(checkpointStore));
    }

    static IMessageConsumer GetConsumer(IMessageConsumer inner)
        => new FilterConsumer(
            new ConcurrentConsumer(inner, 1, 1),
            ctx => !ctx.EventType.StartsWith("$")
        );

    protected ValueTask HandleInt(IMessageConsumeContext context, CancellationToken cancellationToken) {
        var ctx = new DelayedAckConsumeContext(Ack, context);
        return Handler(ctx, cancellationToken);

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
        Subscription?.Dispose();
        return default;
    }

    protected global::EventStore.Client.StreamSubscription? Subscription { get; set; }
}