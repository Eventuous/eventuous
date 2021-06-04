using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Producers;
using Eventuous.Subscriptions;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Shovel {
    /// <summary>
    /// Super-simple shovel, which allows to use a subscription to receive events, and then
    /// shovel them as-is, or after a transformation, to a producer. For example, you can
    /// publish events from EventStoreDB to Google PubSub. It isn't very fast as it acks each
    /// event to the subscription, and, therefore, cannot batch messages without a risk of losing them.
    /// </summary>
    /// <typeparam name="TSubscription">Subscription service, the source of event</typeparam>
    /// <typeparam name="TProducer">Producer, which will publish events</typeparam>
    [PublicAPI]
    public class ShovelService<TSubscription, TProducer> : IHostedService
        where TSubscription : SubscriptionService
        where TProducer : class, IEventProducer {
        readonly TSubscription _subscription;
        readonly TProducer     _producer;

        public record ShovelMessage(string TargetStream, object? Message);

        public delegate ValueTask<ShovelMessage> RouteAndTransform(object message);

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
                    new ShovelHandler<TSubscription, TProducer>(
                        subscriptionId,
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
                    new ShovelHandler<TSubscription, TProducer>(
                        subscriptionId,
                        producer,
                        new DefaultRoute(Ensure.NotNull(targetStream, nameof(targetStream))).Route
                    )
                },
                eventSerializer ?? DefaultEventSerializer.Instance,
                loggerFactory,
                measure
            );
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            await _producer.Initialize(cancellationToken).NoContext();
            await _subscription.StartAsync(cancellationToken).NoContext();
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            await _subscription.StopAsync(cancellationToken).NoContext();
            await _producer.Shutdown(cancellationToken).NoContext();
        }

        public class DefaultRoute {
            readonly string _targetStream;

            public DefaultRoute(string targetStream) => _targetStream = targetStream;

            public ValueTask<ShovelMessage> Route(object message) => new(new ShovelMessage(_targetStream, message));
        }
    }

    class ShovelHandler<TSubscription, TProducer> : IEventHandler
        where TProducer : class, IEventProducer
        where TSubscription : SubscriptionService {
        readonly TProducer                                                 _eventProducer;
        readonly ShovelService<TSubscription, TProducer>.RouteAndTransform _transform;

        public string SubscriptionId { get; }

        public ShovelHandler(
            string                                                    subscriptionId,
            TProducer                                                 eventProducer,
            ShovelService<TSubscription, TProducer>.RouteAndTransform transform
        ) {
            _eventProducer = eventProducer;
            _transform     = transform;
            SubscriptionId = subscriptionId;
        }

        public async Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) {
            var (targetStream, message) = await _transform(evt).NoContext();
            if (message == null) return;
            await _eventProducer.Produce(targetStream, new[] { message }, cancellationToken).NoContext();
        }
    }
}