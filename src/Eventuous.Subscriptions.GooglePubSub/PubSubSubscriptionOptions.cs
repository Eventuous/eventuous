using Google.Cloud.PubSub.V1;
using JetBrains.Annotations;

namespace Eventuous.Subscriptions.GooglePubSub {
    [PublicAPI]
    public class PubSubSubscriptionOptions {
        
        /// <summary>
        /// <see cref="ClientCreationSettings"/> for the <seealso cref="SubscriberClient"/> creation
        /// </summary>
        public SubscriberClient.ClientCreationSettings? ClientCreationSettings { get; init; }

        /// <summary>
        /// <see cref="Settings"/> of the <seealso cref="SubscriberClient"/>
        /// </summary>
        public SubscriberClient.Settings? Settings { get; init; }
        
        public PushConfig? PushConfig { get; init; }

        public int AckDeadline { get; init; } = 60;
    }
}