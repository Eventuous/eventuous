using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

// ReSharper disable InvertIf

namespace Eventuous.Producers.GooglePubSub {
    /// <summary>
    /// Producer for Google PubSub
    /// </summary>
    [PublicAPI]
    public class GooglePubSubProducer : BaseProducer<PubSubProduceOptions> {
        readonly IEventSerializer _serializer;
        readonly ClientCache      _clientCache;
        readonly ILogger?         _log;

        /// <summary>
        /// Create a new instance of a Google PubSub producer
        /// </summary>
        /// <param name="projectId">GCP project ID</param>
        /// <param name="topicId">Google PubSup topic ID (within the project). The topic must be created upfront.</param>
        /// <param name="serializer">Event serializer instance</param>
        /// <param name="options">PubSub producer options</param>
        /// <param name="loggerFactory">Logger factory</param>
        public GooglePubSubProducer(
            string                 projectId,
            string                 topicId,
            IEventSerializer       serializer,
            PubSubProducerOptions? options       = null,
            ILoggerFactory?        loggerFactory = null
        ) {
            _serializer  = Ensure.NotNull(serializer, nameof(serializer));
            _log         = loggerFactory?.CreateLogger($"Producer:{projectId}:{topicId}");
            _clientCache = new ClientCache(Ensure.NotEmptyString(projectId, nameof(projectId)), options, _log);
        }

        public override Task Initialize(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task Shutdown(CancellationToken cancellationToken = default)
            => Task.WhenAll(_clientCache.GetAllClients().Select(x => x.ShutdownAsync(cancellationToken)));

        protected override async Task ProduceOne(
            string                stream,
            object                message,
            Type                  type,
            PubSubProduceOptions? options,
            CancellationToken     cancellationToken
        ) {
            var client = await _clientCache.GetOrAddPublisher(stream);
            await Produce(client, message, type, options);
        }

        protected override async Task ProduceMany(
            string                stream,
            IEnumerable<object>   messages,
            PubSubProduceOptions? options,
            CancellationToken     cancellationToken
        ) {
            var client = await _clientCache.GetOrAddPublisher(stream);
            await Task.WhenAll(messages.Select(x => Produce(client, x, x.GetType(), options)));
        }

        async Task Produce(
            PublisherClient       client,
            object                message,
            Type                  type,
            PubSubProduceOptions? options
        ) {
            var pubSubMessage = CreateMessage(message, type, options);
            await client.PublishAsync(pubSubMessage);
        }

        PubsubMessage CreateMessage(object message, Type type, PubSubProduceOptions? options) {
            var psm = new PubsubMessage {
                Data        = ByteString.CopyFrom(_serializer.Serialize(message)),
                OrderingKey = options?.OrderingKey ?? "",
                Attributes = {
                    { "contentType", _serializer.ContentType },
                    { "eventType", TypeMap.GetTypeNameByType(type) }
                }
            };

            var attrs = options?.AddAttributes?.Invoke(message);

            if (attrs != null) {
                foreach (var (key, value) in attrs) {
                    psm.Attributes.Add(key, value);
                }
            }

            return psm;
        }
    }
}