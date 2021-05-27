using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlStreamStore;
using SqlStreamStore.Streams;
using SqlStreamStore.Subscriptions;
using Eventuous;
using Eventuous.Subscriptions;

namespace Eventuous.Subscriptions.SqlStreamStore {
    public abstract class AllStreamSubscription : SqlStreamStoreSubscriptionService {
        readonly AllStreamSubscriptionOptions _options;
        const string ContentType = "application/json";

        /// <summary>
        /// Creates SqlStreamStore catch-up subscription service for $all
        /// </summary>
        /// <param name="streamStore">SqlStreamStore instance</param>
        /// <param name="options">Options for the subscription (includes the subscription id)</param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>

        protected AllStreamSubscription(
            IStreamStore streamStore,
            AllStreamSubscriptionOptions options,
            ICheckpointStore checkpointStore,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            ILoggerFactory?            loggerFactory   = null,
            ISubscriptionGapMeasure?   measure         = null
        ) : base(
            streamStore,
            options,
            checkpointStore,
            eventHandlers,
            eventSerializer,
            loggerFactory,
            measure
        ) { 
            _options = options;
        }

        protected override Task<EventSubscription> Subscribe(
            Checkpoint checkpoint,
            CancellationToken cancellationToken
        ) {
            var subscription = StreamStore.SubscribeToAll(
                (long?) checkpoint.Position,
                HandleEvent,
                HandleDrop
            );

            return Task.FromResult(new EventSubscription(SubscriptionId, new Stoppable(() => subscription.Dispose())));
        }

        async Task HandleEvent(
            IAllStreamSubscription subscription,
            StreamMessage streamMessage,
            CancellationToken cancellationToken
        )
            => await Handler(await AsReceivedEvent(streamMessage), cancellationToken);

        void HandleDrop(
            IAllStreamSubscription subscription,
            SubscriptionDroppedReason reason,
            Exception ex
        ) 
            => Dropped(SqlStreamStoreMappings.AsDropReason(reason), ex);

        async Task<ReceivedEvent> AsReceivedEvent(StreamMessage streamMessage) {
            var jsonData = await streamMessage.GetJsonData();
            var byteData = Encoding.UTF8.GetBytes(jsonData);
            
            var evt = DeserializeData(
                ContentType,
                streamMessage.Type,
                byteData,
                streamMessage.StreamId,
                (ulong) streamMessage.Position
            );

            return new ReceivedEvent(
                streamMessage.MessageId.ToString(),
                streamMessage.Type,
                ContentType,
                (ulong) streamMessage.Position,
                (ulong) streamMessage.Position,
                streamMessage.StreamId,
                (ulong) streamMessage.Position,
                streamMessage.CreatedUtc,
                evt
            );
        }
    }
}