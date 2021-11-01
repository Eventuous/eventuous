using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eventuous.Shovel;

[PublicAPI]
public class ShovelService<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions> : SubscriptionHostedService
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TProducer : class, IEventProducer<TProduceOptions>
    where TProduceOptions : class
    where TSubscriptionOptions : SubscriptionOptions {
    readonly TProducer     _producer;

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
        string                             subscriptionId,
        CreateSubscription                 createSubscription,
        TProducer                          producer,
        RouteAndTransform<TProduceOptions> routeAndTransform,
        IEventSerializer?                  eventSerializer    = null,
        ISubscriptionHealth?               subscriptionHealth = null,
        ILoggerFactory?                    loggerFactory      = null
    ) : base(
        createSubscription(
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId)),
            new[] {
                new ShovelHandler<TProducer, TProduceOptions>(
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

    public override async Task StartAsync(CancellationToken cancellationToken) {
        while (!_producer.Ready) {
            await Task.Delay(100, cancellationToken);
        }

        await base.StartAsync(cancellationToken).NoContext();
    }
}

public delegate ValueTask<ShovelContext<TProduceOptions>?> RouteAndTransform<TProduceOptions>(IMessageConsumeContext message);