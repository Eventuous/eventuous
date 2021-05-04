using Google.Cloud.PubSub.V1;

namespace Eventuous.Subscriptions.GooglePubSub {
    public class PubSubSubscriptionOptions {
        
        /// <summary>
        /// <see cref="ClientCreationSettings"/> for the <seealso cref="SubscriberClient"/> creation
        /// </summary>
        public SubscriberClient.ClientCreationSettings? ClientCreationSettings { get; init; }

        /// <summary>
        /// <see cref="Settings"/> of the <seealso cref="SubscriberClient"/>
        /// </summary>
        public SubscriberClient.Settings? Settings { get; init; }
    }
}