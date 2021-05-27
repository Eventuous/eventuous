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
    /// <summary>
    /// Catch-up subscription for SqlStreamStore (https://sqlstreamstore.readthedocs.io), for a specific stream
    /// </summary>
    public abstract class StreamSubscription : SqlStreamStoreSubscriptionService {
        readonly StreamSubscriptionOptions _options;
        const string ContentType = "application/json";

        /// <summary>
        /// Creates a SqlStreamStore catch-up subscription service for a given stream
        /// </summary>
        /// <param name="streamStore">SqlStreamStore instance</param>
        /// <param name="options">Options for the subscription (includes the name of the stream to receive events from, and the subscription id)</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        /// <param name="throwOnError"></param>
        protected StreamSubscription(
            IStreamStore                streamStore,
            StreamSubscriptionOptions   options,
            ICheckpointStore            checkpointStore,
            IEnumerable<IEventHandler>  eventHandlers,
            IEventSerializer?           eventSerializer = null,
            ILoggerFactory?             loggerFactory   = null,
            ISubscriptionGapMeasure?    measure         = null
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
            var subscription = StreamStore.SubscribeToStream(
                new StreamId(_options.StreamName),
                (int?) checkpoint.Position,
                HandleEvent,
                HandleDrop
            );
            return Task.FromResult(new EventSubscription(SubscriptionId, new Stoppable(() => subscription.Dispose())));
        }

        async Task HandleEvent(
            IStreamSubscription subscription,
            StreamMessage streamMessage,
            CancellationToken cancellationToken
        )
            => await Handler(await AsReceivedEvent(streamMessage), cancellationToken);

        void HandleDrop(
            IStreamSubscription subscription,
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