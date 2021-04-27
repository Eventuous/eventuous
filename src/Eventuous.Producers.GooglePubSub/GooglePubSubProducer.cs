using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using JetBrains.Annotations;
using static Google.Cloud.PubSub.V1.PublisherClient;

namespace Eventuous.Producers.GooglePubSub {
    /// <summary>
    /// Producer for Google PubSub
    /// </summary>
    [PublicAPI]
    public class GooglePubSubProducer : BaseProducer {
        readonly IEventSerializer _serializer;
        readonly PublisherClient  _client;
        readonly string?          _orderingKey;

        /// <summary>
        /// Create a new instance of a Google PubSub producer
        /// </summary>
        /// <param name="projectId">GCP project ID</param>
        /// <param name="topicId">Google PubSup topic ID (within the project). The topic must be created upfront.</param>
        /// <param name="serializer">Event serializer instance</param>
        /// <param name="clientCreationSettings">Optional: <see cref="ClientCreationSettings"/> for the <seealso cref="PublisherClient"/> creation</param>
        /// <param name="settings">Optional: <see cref="Settings"/> of the <seealso cref="PublisherClient"/></param>
        /// <param name="orderingKey">Optional: ordering key for the producer. Ordering must be enabled in the <seealso cref="Settings"/></param>
        public GooglePubSubProducer(
            string                  projectId,
            string                  topicId,
            IEventSerializer        serializer,
            ClientCreationSettings? clientCreationSettings = null,
            Settings?               settings               = null,
            string?                 orderingKey            = null
        ) {
            _serializer = Ensure.NotNull(serializer, nameof(serializer));

            var topicName = TopicName.FromProjectTopic(
                Ensure.NotEmptyString(projectId, nameof(projectId)),
                Ensure.NotEmptyString(topicId, nameof(topicId))
            );

            if (settings?.EnableMessageOrdering == true) {
                _orderingKey = Ensure.NotEmptyString(orderingKey, nameof(orderingKey));
            }

            _client = Create(topicName, clientCreationSettings, settings);
        }

        protected override Task ProduceOne(object message, Type type, CancellationToken cancellationToken) {
            var pubSubMessage = CreateMessage(message, type);

            return _orderingKey == null
                ? _client.PublishAsync(pubSubMessage)
                : _client.PublishAsync(_orderingKey, pubSubMessage);
        }

        protected override Task ProduceMany(IEnumerable<object> messages, CancellationToken cancellationToken)
            => Task.WhenAll(messages.Select(x => ProduceOne(x, x.GetType(), cancellationToken)));

        PubsubMessage CreateMessage(object message, Type type) {
            return new() {
                Data = ByteString.CopyFrom(_serializer.Serialize(message)),
                Attributes = {
                    { "contentType", _serializer.ContentType },
                    { "eventType", TypeMap.GetTypeNameByType(type) }
                }
            };
        }
    }
}