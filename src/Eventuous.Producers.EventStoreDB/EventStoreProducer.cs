using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;

namespace Eventuous.Producers.EventStoreDB {
    /// <summary>
    /// Producer for EventStoreDB
    /// </summary>
    [PublicAPI]
    public class EventStoreProducer : BaseProducer<EventStoreProduceOptions> {
        readonly string           _stream;
        readonly EventStoreClient _client;
        readonly IEventSerializer _serializer;

        /// <summary>
        /// Create a new EventStoreDB producer instance
        /// </summary>
        /// <param name="eventStoreClient">EventStoreDB gRPC client</param>
        /// <param name="stream">Stream name, where the events will be produced</param>
        /// <param name="serializer">Event serializer instance</param>
        public EventStoreProducer(EventStoreClient eventStoreClient, string stream, IEventSerializer serializer) {
            _client     = Ensure.NotNull(eventStoreClient, nameof(eventStoreClient));
            _stream     = Ensure.NotEmptyString(stream, nameof(stream));
            _serializer = Ensure.NotNull(serializer, nameof(serializer));
        }

        /// <summary>
        /// Create a new EventStoreDB producer instance
        /// </summary>
        /// <param name="clientSettings">EventStoreDB gRPC client settings</param>
        /// <param name="stream">Stream name, where the events will be produced</param>
        /// <param name="serializer">Event serializer instance</param>
        public EventStoreProducer(EventStoreClientSettings clientSettings, string stream, IEventSerializer serializer)
            : this(new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings))), stream, serializer) { }

        public override Task Initialize(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override Task ProduceMany(
            IEnumerable<object>       messages,
            EventStoreProduceOptions? options,
            CancellationToken         cancellationToken
        ) {
            var data = Ensure.NotNull(messages, nameof(messages))
                .Select(x => CreateMessage(x, x.GetType(), options?.Metadata));

            return _client.AppendToStreamAsync(
                _stream,
                options?.ExpectedState ?? StreamState.Any,
                data,
                options?.ConfigureOperation,
                options?.Credentials,
                cancellationToken
            );
        }

        protected override Task ProduceOne(
            object                    message,
            Type                      type,
            EventStoreProduceOptions? options,
            CancellationToken         cancellationToken
        ) {
            var eventData = CreateMessage(message, type, options?.Metadata);

            return _client.AppendToStreamAsync(
                _stream,
                options?.ExpectedState ?? StreamState.Any,
                new[] { eventData },
                options?.ConfigureOperation,
                options?.Credentials,
                cancellationToken
            );
        }

        EventData CreateMessage(object message, Type type, object? metadata) {
            var msg       = Ensure.NotNull(message, nameof(message));
            var typeName  = TypeMap.GetTypeNameByType(type);
            var metaBytes = metadata == null ? null : _serializer.Serialize(metadata);

            return new EventData(
                Uuid.NewUuid(),
                typeName,
                _serializer.Serialize(msg),
                metaBytes,
                _serializer.ContentType
            );
        }
    }
}