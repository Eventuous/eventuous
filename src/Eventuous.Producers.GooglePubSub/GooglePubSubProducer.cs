using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Google.Cloud.PubSub.V1.PublisherClient;

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
        /// <param name="serializer">Event serializer instance</param>
        /// <param name="settings"></param>
        /// <param name="loggerFactory">Logger factory</param>
        /// <param name="clientCreationSettings"></param>
        public GooglePubSubProducer(
            string                  projectId,
            IEventSerializer?       serializer             = null,
            ClientCreationSettings? clientCreationSettings = null,
            Settings?               settings               = null,
            ILoggerFactory?         loggerFactory          = null
        ) : this(
            new PubSubProducerOptions {
                ProjectId              = Ensure.NotEmptyString(projectId, nameof(projectId)),
                Settings               = settings,
                ClientCreationSettings = clientCreationSettings
            },
            serializer,
            loggerFactory
        ) { }

        /// <summary>
        /// Create a new instance of a Google PubSub producer
        /// </summary>
        /// <param name="options">Producer options</param>
        /// <param name="serializer">Optional: event serializer. Will use the default instance if missing.</param>
        /// <param name="loggerFactory">Logger factory</param>
        public GooglePubSubProducer(
            PubSubProducerOptions options,
            IEventSerializer?     serializer    = null,
            ILoggerFactory?       loggerFactory = null
        ) {
            Ensure.NotNull(options, nameof(options));

            _serializer  = serializer ?? DefaultEventSerializer.Instance;
            _log         = loggerFactory?.CreateLogger($"Producer:{options.ProjectId}");
            _clientCache = new ClientCache(options, _log);
        }

        public GooglePubSubProducer(
            IOptions<PubSubProducerOptions> options,
            IEventSerializer?               serializer    = null,
            ILoggerFactory?                 loggerFactory = null
        ) : this(options.Value, serializer, loggerFactory) { }

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
            var client = await _clientCache.GetOrAddPublisher(stream, cancellationToken).NoContext();
            await Produce(client, message, type, options).NoContext();
        }

        protected override async Task ProduceMany(
            string                stream,
            IEnumerable<object>   messages,
            PubSubProduceOptions? options,
            CancellationToken     cancellationToken
        ) {
            var client = await _clientCache.GetOrAddPublisher(stream, cancellationToken).NoContext();
            await Task.WhenAll(messages.Select(x => Produce(client, x, x.GetType(), options)));
        }

        async Task Produce(
            PublisherClient       client,
            object                message,
            Type                  type,
            PubSubProduceOptions? options
        ) {
            var pubSubMessage = CreateMessage(message, type, options);
            await client.PublishAsync(pubSubMessage).NoContext();
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