using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    /// <summary>
    /// Persistent subscription for EventStoreDB, for a specific stream
    /// </summary>
    [PublicAPI]
    public class StreamPersistentSubscription : EventStoreSubscriptionService {
        public delegate Task HandleEventProcessingFailure(
            EventStoreClient       client,
            PersistentSubscription subscription,
            ResolvedEvent          resolvedEvent,
            Exception              exception
        );

        readonly EventStorePersistentSubscriptionsClient _subscriptionClient;
        readonly StreamPersistentSubscriptionOptions     _options;
        readonly HandleEventProcessingFailure            _handleEventProcessingFailure;

        public StreamPersistentSubscription(
            EventStoreClient                    eventStoreClient,
            StreamPersistentSubscriptionOptions options,
            IEnumerable<IEventHandler>          eventHandlers,
            IEventSerializer?                   eventSerializer = null,
            ILoggerFactory?                     loggerFactory   = null,
            ISubscriptionGapMeasure?            measure         = null
        ) : base(
            eventStoreClient,
            options,
            new NoOpCheckpointStore(),
            eventHandlers,
            eventSerializer,
            loggerFactory,
            measure
        ) {
            Ensure.NotEmptyString(options.Stream, nameof(options.Stream));

            var settings   = eventStoreClient.GetSettings().Copy();
            var opSettings = settings.OperationOptions.Clone();
            options.ConfigureOperation?.Invoke(opSettings);
            settings.OperationOptions = opSettings;

            _subscriptionClient           = new EventStorePersistentSubscriptionsClient(settings);
            _handleEventProcessingFailure = options.FailureHandler ?? DefaultEventProcessingFailureHandler;
            _options                      = options;
        }

        /// <summary>
        /// Creates EventStoreDB persistent subscription service for a given stream
        /// </summary>
        /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
        /// <param name="streamName">Name of the stream to receive events from</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamPersistentSubscription(
            EventStoreClient           eventStoreClient,
            string                     streamName,
            string                     subscriptionId,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            ILoggerFactory?            loggerFactory   = null,
            ISubscriptionGapMeasure?   measure         = null
        ) : this(
            eventStoreClient,
            new StreamPersistentSubscriptionOptions {
                Stream         = streamName,
                SubscriptionId = subscriptionId
            },
            eventHandlers,
            eventSerializer,
            loggerFactory,
            measure
        ) { }

        protected override async Task<EventSubscription> Subscribe(
            Checkpoint        _,
            CancellationToken cancellationToken
        ) {
            var settings = _options.SubscriptionSettings ?? new PersistentSubscriptionSettings(_options.ResolveLinkTos);
            var autoAck  = _options.AutoAck;

            PersistentSubscription sub;

            try {
                sub = await LocalSubscribe().Ignore();
            }
            catch (PersistentSubscriptionNotFoundException) {
                await _subscriptionClient.CreateAsync(
                    _options.Stream,
                    SubscriptionId,
                    settings,
                    _options.Credentials,
                    cancellationToken
                ).Ignore();

                sub = await LocalSubscribe().Ignore();
            }

            return new EventSubscription(SubscriptionId, new Stoppable(() => sub.Dispose()));

            void HandleDrop(PersistentSubscription __, SubscriptionDroppedReason reason, Exception? exception)
                => Dropped(EsdbMappings.AsDropReason(reason), exception);

            async Task HandleEvent(
                PersistentSubscription subscription,
                ResolvedEvent          re,
                int?                   retryCount,
                CancellationToken      ct
            ) {
                var receivedEvent = AsReceivedEvent(re);

                try {
                    await Handler(receivedEvent, ct).Ignore();

                    if (!autoAck)
                        await subscription.Ack(re).Ignore();
                }
                catch (Exception e) {
                    await _handleEventProcessingFailure(EventStoreClient, subscription, re, e).Ignore();
                }
            }

            Task<PersistentSubscription> LocalSubscribe()
                => _subscriptionClient.SubscribeAsync(
                    _options.Stream,
                    SubscriptionId,
                    HandleEvent,
                    HandleDrop,
                    _options.Credentials,
                    _options.BufferSize,
                    _options.AutoAck,
                    cancellationToken
                );

            static ReceivedEvent AsReceivedEvent(ResolvedEvent re)
                => new() {
                    EventId        = re.Event.EventId.ToString(),
                    GlobalPosition = re.Event.Position.CommitPosition,
                    Stream         = re.OriginalStreamId,
                    StreamPosition = re.Event.EventNumber,
                    Sequence       = re.Event.EventNumber,
                    Created        = re.Event.Created,
                    EventType      = re.Event.EventType,
                    Data           = re.Event.Data,
                    Metadata       = re.Event.Metadata
                };
        }

        static Task DefaultEventProcessingFailureHandler(
            EventStoreClient       client,
            PersistentSubscription subscription,
            ResolvedEvent          resolvedEvent,
            Exception              exception
        )
            => subscription.Nack(PersistentSubscriptionNakEventAction.Retry, exception.Message, resolvedEvent);
    }
}