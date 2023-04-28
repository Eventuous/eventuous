using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Persistent subscription for EventStoreDB, for $all stream
/// </summary>
public class AllPersistentSubscription : PersistentSubscriptionBase<AllPersistentSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Persistent subscription for EventStoreDB, for $all stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB client instance</param>
    /// <param name="options">Persistent subscription options</param>
    /// <param name="consumePipe">Consume pipe, usually provided by the builder</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    public AllPersistentSubscription(
        EventStoreClient                 eventStoreClient,
        AllPersistentSubscriptionOptions options,
        ConsumePipe                      consumePipe,
        ILoggerFactory?                  loggerFactory
    ) : base(eventStoreClient, options, consumePipe, loggerFactory) { }

    /// <summary>
    /// Creates EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="consumerPipe"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="loggerFactory"></param>
    public AllPersistentSubscription(
        EventStoreClient     eventStoreClient,
        string               subscriptionId,
        ConsumePipe          consumerPipe,
        IEventSerializer?    eventSerializer = null,
        IMetadataSerializer? metaSerializer  = null,
        ILoggerFactory?      loggerFactory   = null
    ) : this(
        eventStoreClient,
        new AllPersistentSubscriptionOptions {
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer
        },
        consumerPipe,
        loggerFactory
    ) { }

    /// <summary>
    /// Creates EventStoreDB persistent subscription consumer group for $all
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override Task CreatePersistentSubscription(PersistentSubscriptionSettings settings, CancellationToken cancellationToken)
        => SubscriptionClient.CreateToAllAsync(
            Options.SubscriptionId,
            settings,
            Options.Deadline,
            Options.Credentials,
            cancellationToken
        );

    /// <summary>
    /// Internal method to start the subscription
    /// </summary>
    /// <param name="eventAppeared">Event processing delegate</param>
    /// <param name="subscriptionDropped">Drop handler</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override Task<PersistentSubscription> LocalSubscribe(
        Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
        Action<PersistentSubscription, SubscriptionDroppedReason, Exception?>?     subscriptionDropped,
        CancellationToken                                                          cancellationToken
    )
        => SubscriptionClient.SubscribeToAllAsync(
            Options.SubscriptionId,
            eventAppeared,
            subscriptionDropped,
            Options.Credentials,
            Options.BufferSize,
            cancellationToken
        );

    /// <summary>
    /// Gets the position of the event in the stream
    /// </summary>
    /// <param name="re"></param>
    /// <returns></returns>
    protected override ulong GetContextStreamPosition(ResolvedEvent re)
        => re.Event.Position.CommitPosition;

    /// <summary>
    /// Returns a measure callback for the subscription
    /// </summary>
    /// <returns></returns>
    public GetSubscriptionEndOfStream GetMeasure()
        => new AllStreamSubscriptionMeasure(Options.SubscriptionId, EventStoreClient).GetEndOfStream;
}
