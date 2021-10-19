using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Monitoring;
using Microsoft.Extensions.Logging;

namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract class EventStoreCatchUpSubscriptionBase<T> : EventStoreSubscriptionBase<T>
    where T : EventStoreSubscriptionOptions {
    protected ICheckpointStore    CheckpointStore  { get; }

    protected EventStoreCatchUpSubscriptionBase(
        EventStoreClient           eventStoreClient,
        T                          options,
        ICheckpointStore           checkpointStore,
        IEnumerable<IEventHandler> eventHandlers,
        ILoggerFactory?            loggerFactory = null,
        ISubscriptionGapMeasure?   measure       = null
    ) : base(
        eventStoreClient,
        options,
        eventHandlers,
        loggerFactory,
        measure
    ) {
        CheckpointStore  = Ensure.NotNull(checkpointStore, nameof(checkpointStore));
    }

    protected async Task<Checkpoint> GetCheckpoint(CancellationToken cancellationToken) {
        var last = GetLastProcessedEventPosition();
        if (IsRunning && last != null) {
            return new Checkpoint(Options.SubscriptionId, last.Position);
        }

        var checkpoint = await CheckpointStore
            .GetLastCheckpoint(Options.SubscriptionId, cancellationToken)
            .NoContext();

        SetLastProcessedEventPosition(new EventPosition(checkpoint.Position, DateTime.Now));

        return checkpoint;
    }

    protected override async Task Handler(ReceivedEvent re, CancellationToken cancellationToken) {
        if (re.EventType.StartsWith("$")) {
            SetLastProcessedEventPosition(EventPosition.FromReceivedEvent(re));
            await Store().NoContext();
            return;
        }

        await base.Handler(re, cancellationToken);

        await Store().NoContext();

        Task Store() => StoreCheckpoint(EventPosition.FromReceivedEvent(re), cancellationToken);
    }

    protected async Task StoreCheckpoint(
        EventPosition     position,
        CancellationToken cancellationToken
    ) {
        SetLastProcessedEventPosition(position);
        var checkpoint = new Checkpoint(Options.SubscriptionId, position.Position);
        await CheckpointStore.StoreCheckpoint(checkpoint, cancellationToken).NoContext();
    }
    
    protected override ValueTask Unsubscribe(CancellationToken cancellationToken) {
        Subscription?.Dispose();
        return default;
    }

    protected global::EventStore.Client.StreamSubscription? Subscription { get; set; }
}