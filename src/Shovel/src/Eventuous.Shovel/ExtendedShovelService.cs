using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Shovel;

[PublicAPI]
public class ShovelService<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions> : IHostedService
    where TSubscription : SubscriptionService<TSubscriptionOptions>
    where TProducer : class, IEventProducer<TProduceOptions>
    where TProduceOptions : class
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
        string                             subscriptionId,
        CreateSubscription                 createSubscription,
        TProducer                          producer,
        RouteAndTransform<TProduceOptions> routeAndTransform,
        IEventSerializer?                  eventSerializer = null,
        ILoggerFactory?                    loggerFactory   = null,
        SubscriptionGapMeasure?            measure         = null
    ) {
        _producer = Ensure.NotNull(producer, nameof(producer));

        _subscription = createSubscription(
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId)),
            new[] {
                new ShovelHandler<TProducer, TProduceOptions>(
                    producer,
                    Ensure.NotNull(routeAndTransform, nameof(routeAndTransform))
                )
            },
            eventSerializer,
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

    public Task StopAsync(CancellationToken cancellationToken)
        => _subscription.StopAsync(cancellationToken);
}

public delegate ValueTask<ShovelMessage<TProduceOptions>?> RouteAndTransform<TProduceOptions>(
    ReceivedEvent message
);