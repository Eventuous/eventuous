using Google.Cloud.PubSub.V1;
using static Google.Cloud.PubSub.V1.PublisherClient;

namespace Eventuous.Producers.GooglePubSub {
    public class PubSubProducerOptions {
        /// <summary>
        /// <see cref="ClientCreationSettings"/> for the <seealso cref="Publisher.PublisherClient"/> creation
        /// </summary>
        public ClientCreationSettings? ClientCreationSettings { get; init; }

        /// <summary>
        /// <see cref="PublisherClient.Settings"/> of the <seealso cref="PublisherClient"/>
        /// </summary>
        public Settings? Settings { get; init; }
    }
}