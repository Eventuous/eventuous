using Eventuous.Subscriptions.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Shovel;

/// <summary>
/// Super-simple shovel, which allows to use a subscription to receive events, and then
/// shovel them as-is, or after a transformation, to a producer. For example, you can
/// publish events from EventStoreDB to Google PubSub. It isn't very fast as it acks each
/// event to the subscription, and, therefore, cannot batch messages without a risk of losing them.
/// </summary>
/// <typeparam name="TSubscription">Subscription service, the source of event</typeparam>
/// <typeparam name="TProducer">Producer, which will publish events</typeparam>
/// <typeparam name="TSubscriptionOptions">Subscription options type</typeparam>
[PublicAPI]
public class ShovelService<TSubscription, TSubscriptionOptions, TProducer> : IHostedService
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TProducer : class, IEventProducer
    where TSubscriptionOptions : SubscriptionOptions {
    readonly TSubscription _subscription;
    readonly TProducer     _producer;

    public delegate TSubscription CreateSubscription(
        string                     subscriptionId,
        IEnumerable<IEventHandler> eventHandlers,
        IEventSerializer?          serializer,
        ILoggerFactory?            loggerFactory,
        SubscriptionGapMeasure?    measure
    );

    /// <summary>
    /// Creates a shovel service instance, which must be registered as a hosted service
    /// </summary>
    /// <param name="subscriptionId">Shovel subscription id</param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="createSubscription">Function to create a subscription</param>
    /// <param name="producer">Producer instance</param>
    /// <param name="routeAndTransform">Routing and transformation function</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="measure">Subscription gap measurement</param>
    public ShovelService(
        string                  subscriptionId,
        CreateSubscription      createSubscription,
        TProducer               producer,
        RouteAndTransform       routeAndTransform,
        IEventSerializer?       eventSerializer = null,
        ILoggerFactory?         loggerFactory   = null,
        SubscriptionGapMeasure? measure         = null
    ) {
        _producer = Ensure.NotNull(producer, nameof(producer));

        _subscription = createSubscription(
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId)),
            new[] {
                new ShovelHandler<TProducer>(
                    producer,
                    Ensure.NotNull(routeAndTransform, nameof(routeAndTransform))
                )
            },
            eventSerializer,
            loggerFactory,
            measure
        );
    }

    /// <summary>
    /// Creates a shovel service instance, which must be registered as a hosted service
    /// </summary>
    /// <param name="subscriptionId">Shovel subscription id</param>
    /// <param name="createSubscription">Function to create a subscription</param>
    /// <param name="targetStream">The stream where events will be produced</param>
    /// <param name="producer">Producer instance</param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="measure">Subscription gap measurement</param>
    public ShovelService(
        string                  subscriptionId,
        CreateSubscription      createSubscription,
        TProducer               producer,
        string                  targetStream,
        IEventSerializer?       eventSerializer = null,
        ILoggerFactory?         loggerFactory   = null,
        SubscriptionGapMeasure? measure         = null
    ) {
        _producer = Ensure.NotNull(producer, nameof(producer));

        _subscription = createSubscription(
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId)),
            new[] {
                new ShovelHandler<TProducer>(
                    producer,
                    new DefaultRoute(Ensure.NotNull(targetStream, nameof(targetStream))).Route!
                )
            },
            eventSerializer ?? DefaultEventSerializer.Instance,
            loggerFactory,
            measure
        );
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        while (!_producer.Ready) {
            await Task.Delay(100, cancellationToken);
        }

        await _subscription.StartAsync(cancellationToken).NoContext();
    }

    public Task StopAsync(CancellationToken cancellationToken) => _subscription.StopAsync(cancellationToken);

    public class DefaultRoute {
        readonly string _targetStream;

        public DefaultRoute(string targetStream)
            => _targetStream = targetStream;

        public ValueTask<ShovelMessage> Route(object message)
            => new(new ShovelMessage(_targetStream, message));
    }
}

public delegate ValueTask<ShovelMessage?> RouteAndTransform(object message);