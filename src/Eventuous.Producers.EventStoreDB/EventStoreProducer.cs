using System;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;

namespace Eventuous.Producers.EventStoreDB {
    [PublicAPI]
    public class EventStoreProducer : BaseProducer {
        readonly string           _stream;
        readonly EventStoreClient _client;
        readonly IEventSerializer _serializer;

        public EventStoreProducer(EventStoreClient client, string stream, IEventSerializer serializer) {
            _client     = Ensure.NotNull(client, nameof(client));
            _stream     = Ensure.NotEmptyString(stream, nameof(stream));
            _serializer = Ensure.NotNull(serializer, nameof(serializer));
        }

        protected override async Task Produce(object message, Type type) {
            var msg      = Ensure.NotNull(message, nameof(message));
            var typeName = TypeMap.GetTypeNameByType(type);

            var eventData = new EventData(
                Uuid.NewUuid(),
                typeName,
                _serializer.Serialize(msg),
                null,
                _serializer.ContentType
            );

            await _client.AppendToStreamAsync(_stream, StreamState.Any, new[] { eventData });
        }
    }
}