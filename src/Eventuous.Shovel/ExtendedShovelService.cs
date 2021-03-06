using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Producers;
using Eventuous.Subscriptions;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Shovel {
    [PublicAPI]
    public class ShovelService<TSubscription, TProducer, TProduceOptions> : IHostedService
        where TSubscription : SubscriptionService
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class {
        readonly TSubscription _subscription;
        readonly TProducer     _producer;

        public record ShovelMessage(string TargetStream, object? Message, TProduceOptions ProduceOptions);

        public delegate ValueTask<ShovelMessage?> RouteAndTransform(object message);

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
                    new ShovelHandler<TSubscription, TProducer, TProduceOptions>(
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

        public async Task StartAsync(CancellationToken cancellationToken) {
            await _producer.Initialize(cancellationToken).NoContext();
            await _subscription.StartAsync(cancellationToken).NoContext();
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            await _subscription.StopAsync(cancellationToken).NoContext();
            await _producer.Shutdown(cancellationToken).NoContext();
        }
    }

    class ShovelHandler<TSubscription, TProducer, TProduceOptions> : IEventHandler
        where TProducer : class, IEventProducer<TProduceOptions>
        where TSubscription : SubscriptionService
        where TProduceOptions : class {
        readonly TProducer _eventProducer;

        readonly ShovelService<TSubscription, TProducer, TProduceOptions>.RouteAndTransform _transform;

        public string SubscriptionId { get; }

        public ShovelHandler(
            string                                                                     subscriptionId,
            TProducer                                                                  eventProducer,
            ShovelService<TSubscription, TProducer, TProduceOptions>.RouteAndTransform transform
        ) {
            _eventProducer = eventProducer;
            _transform     = transform;
            SubscriptionId = subscriptionId;
        }

        public async Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) {
            var shovelMessage = await _transform(evt).NoContext();
            if (shovelMessage?.Message == null) return;

            await _eventProducer.Produce(
                shovelMessage.TargetStream,
                new[] { shovelMessage.Message },
                shovelMessage.ProduceOptions,
                cancellationToken
            ).NoContext();
        }
    }
}