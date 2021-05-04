using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Producers;
using Eventuous.Subscriptions;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Shovel {
    public delegate ValueTask<object> Transform(object message);

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
        where TProducer : IEventProducer {
        readonly TSubscription _subscription;
        readonly TProducer     _producer;

        public delegate TSubscription CreateSubscription(
            string                     subscriptionId,
            IEventSerializer           serializer,
            IEnumerable<IEventHandler> eventHandlers,
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
        /// <param name="transform">Optional transformation function</param>
        /// <param name="loggerFactory">Logger factory</param>
        /// <param name="measure">Subscription gap measurement</param>
        public ShovelService(
            string                  subscriptionId,
            IEventSerializer        eventSerializer,
            CreateSubscription      createSubscription,
            TProducer               producer,
            Transform?              transform     = null,
            ILoggerFactory?         loggerFactory = null,
            SubscriptionGapMeasure? measure       = null
        ) {
            _producer = producer;

            _subscription = createSubscription(
                subscriptionId,
                eventSerializer,
                new[] { new ShovelHandler<TProducer>(subscriptionId, producer, transform) },
                loggerFactory,
                measure
            );
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            await _producer.Initialize(cancellationToken);
            await _subscription.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            await _subscription.StopAsync(cancellationToken);
            await _producer.Shutdown(cancellationToken);
        }
    }

    class ShovelHandler<TProducer> : IEventHandler where TProducer : IEventProducer {
        readonly TProducer  _eventProducer;
        readonly Transform? _transform;

        public string SubscriptionId { get; }

        public ShovelHandler(
            string     subscriptionId,
            TProducer  eventProducer,
            Transform? transform
        ) {
            _eventProducer = eventProducer;
            _transform     = transform;
            SubscriptionId = subscriptionId;
        }

        public async Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) {
            var transformed = _transform == null ? evt : await _transform(evt);
            await _eventProducer.Produce(new[] { transformed }, cancellationToken);
        }
    }
}