using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
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
public class ShovelService<TSubscription, TSubscriptionOptions, TProducer> : SubscriptionHostedService
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TProducer : class, IEventProducer
    where TSubscriptionOptions : SubscriptionOptions {
    readonly TProducer _producer;

    public delegate TSubscription CreateSubscription(
        string                     subscriptionId,
        IEnumerable<IEventHandler> eventHandlers,
        IEventSerializer?          serializer,
        ILoggerFactory?            loggerFactory
    );

    /// <summary>
    /// Creates a shovel service instance, which must be registered as a hosted service
    /// </summary>
    /// <param name="subscriptionId">Shovel subscription id</param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="createSubscription">Function to create a subscription</param>
    /// <param name="producer">Producer instance</param>
    /// <param name="routeAndTransform">Routing and transformation function</param>
    /// <param name="subscriptionHealth"></param>
    /// <param name="loggerFactory">Logger factory</param>
    public ShovelService(
        string                  subscriptionId,
        CreateSubscription      createSubscription,
        TProducer               producer,
        RouteAndTransform       routeAndTransform,
        IEventSerializer?       eventSerializer    = null,
        ISubscriptionHealth?    subscriptionHealth = null,
        ILoggerFactory?         loggerFactory      = null
    ) : base(
        createSubscription(
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId)),
            new[] {
                new ShovelHandler<TProducer>(
                    producer,
                    Ensure.NotNull(routeAndTransform, nameof(routeAndTransform))
                )
            },
            eventSerializer,
            loggerFactory
        ),
        subscriptionHealth,
        loggerFactory
    ) {
        _producer = Ensure.NotNull(producer, nameof(producer));
    }

    /// <summary>
    /// Creates a shovel service instance, which must be registered as a hosted service
    /// </summary>
    /// <param name="subscriptionId">Shovel subscription id</param>
    /// <param name="createSubscription">Function to create a subscription</param>
    /// <param name="targetStream">The stream where events will be produced</param>
    /// <param name="producer">Producer instance</param>
    /// <param name="eventSerializer">Event serializer</param>
    /// <param name="subscriptionHealth"></param>
    /// <param name="loggerFactory">Logger factory</param>
    public ShovelService(
        string                  subscriptionId,
        CreateSubscription      createSubscription,
        TProducer               producer,
        StreamName              targetStream,
        IEventSerializer?       eventSerializer    = null,
        ISubscriptionHealth?    subscriptionHealth = null,
        ILoggerFactory?         loggerFactory      = null
    ) : base(
        createSubscription(
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId)),
            new[] {
                new ShovelHandler<TProducer>(
                    producer,
                    new DefaultRoute(Ensure.NotNull(targetStream, nameof(targetStream))).Route!
                )
            },
            eventSerializer ?? DefaultEventSerializer.Instance,
            loggerFactory
        ),
        subscriptionHealth,
        loggerFactory
    ) {
        _producer = Ensure.NotNull(producer, nameof(producer));
    }

    public override async Task StartAsync(CancellationToken cancellationToken) {
        while (!_producer.Ready) {
            await Task.Delay(100, cancellationToken);
        }

        await base.StartAsync(cancellationToken).NoContext();
    }

    public class DefaultRoute {
        readonly StreamName _targetStream;

        public DefaultRoute(StreamName targetStream) => _targetStream = targetStream;

        public ValueTask<ShovelContext> Route(IMessageConsumeContext context)
            => new(new ShovelContext(_targetStream, context.Message, context.Metadata));
    }
}

public delegate ValueTask<ShovelContext?> RouteAndTransform(IMessageConsumeContext context);