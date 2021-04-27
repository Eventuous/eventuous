using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public class AllStreamSubscriptionService : EsdbSubscriptionService {
        readonly IEventFilter _eventFilter;

        public AllStreamSubscriptionService(
            EventStoreClient           eventStoreClient,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            IEventFilter?              eventFilter   = null,
            SubscriptionGapMeasure?    measure       = null
        ) : base(
            eventStoreClient,
            subscriptionId,
            checkpointStore,
            eventSerializer,
            eventHandlers,
            loggerFactory,
            measure
        )
            => _eventFilter = eventFilter ?? EventTypeFilter.ExcludeSystemEvents();

        public AllStreamSubscriptionService(
            EventStoreClientSettings   clientSettings,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            IEventFilter?              eventFilter   = null,
            SubscriptionGapMeasure?    measure       = null
        ) : this(
            new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings))),
            subscriptionId,
            checkpointStore,
            eventSerializer,
            eventHandlers,
            loggerFactory,
            eventFilter,
            measure
        ) { }

        protected override async Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var filterOptions = new SubscriptionFilterOptions(
                _eventFilter,
                10,
                (_, p, ct) => StoreCheckpoint(new EventPosition(p.CommitPosition, DateTime.Now), ct)
            );

            var sub = checkpoint.Position != null
                ? await EventStoreClient.SubscribeToAllAsync(
                    new Position(checkpoint.Position.Value, checkpoint.Position.Value),
                    HandleEvent,
                    false,
                    HandleDrop,
                    filterOptions,
                    cancellationToken: cancellationToken
                )
                : await EventStoreClient.SubscribeToAllAsync(
                    HandleEvent,
                    false,
                    HandleDrop,
                    filterOptions,
                    cancellationToken: cancellationToken
                );

            return new EventSubscription(SubscriptionId, new Stoppable(() => sub.Dispose()));

            Task HandleEvent(StreamSubscription _, ResolvedEvent re, CancellationToken ct)
                => Handler(AsReceivedEvent(re), ct);

            void HandleDrop(StreamSubscription _, SubscriptionDroppedReason reason, Exception? ex)
                => Dropped(EsdbMappings.AsDropReason(reason), ex);
            
            static ReceivedEvent AsReceivedEvent(ResolvedEvent re)
                => new() {
                    EventId        = re.Event.EventId.ToString(),
                    GlobalPosition = re.Event.Position.CommitPosition,
                    StreamPosition = re.Event.Position.CommitPosition,
                    OriginalStream = re.OriginalStreamId,
                    Sequence       = re.Event.EventNumber,
                    Created        = re.Event.Created,
                    EventType      = re.Event.EventType,
                    Data           = re.Event.Data,
                    Metadata       = re.Event.Metadata
                };
        }
    }
}