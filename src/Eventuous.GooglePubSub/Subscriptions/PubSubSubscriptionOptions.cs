using Eventuous.Subscriptions;
using Google.Cloud.PubSub.V1;
using JetBrains.Annotations;
using static Eventuous.GooglePubSub.Subscriptions.GooglePubSubSubscription;
using static Google.Cloud.PubSub.V1.SubscriberClient;

namespace Eventuous.GooglePubSub.Subscriptions {
    [PublicAPI]
    public class PubSubSubscriptionOptions : SubscriptionOptions {
        /// <summary>
        /// Google Cloud project id
        /// </summary>
        public string ProjectId { get; init; } = null!;

        /// <summary>
        /// PubSub topic id
        /// </summary>
        public string TopicId { get; init; } = null!;

        /// <summary>
        /// <see cref="ClientCreationSettings"/> for the <seealso cref="SubscriberClient"/> creation
        /// </summary>
        public ClientCreationSettings? ClientCreationSettings { get; init; }

        /// <summary>
        /// <see cref="Settings"/> of the <seealso cref="SubscriberClient"/>
        /// </summary>
        public Settings? Settings { get; init; }

        public PushConfig? PushConfig { get; init; }

        public int AckDeadline { get; init; } = 60;

        /// <summary>
        /// Custom failure handler, which allows overriding the default behaviour (NACK)
        /// </summary>
        public HandleEventProcessingFailure? FailureHandler { get; init; }
    }
}