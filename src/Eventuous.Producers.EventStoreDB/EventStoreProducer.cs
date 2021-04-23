using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        protected override Task ProduceMany(IEnumerable<object> messages, CancellationToken cancellationToken) {
            var data = Ensure.NotNull(messages, nameof(messages))
                .Select(x => CreateMessage(x, x.GetType()));

            return _client.AppendToStreamAsync(_stream, StreamState.Any, data, cancellationToken: cancellationToken);
        }

        protected override Task ProduceOne(object message, Type type, CancellationToken cancellationToken){
            var eventData = CreateMessage(message, type);

            return _client.AppendToStreamAsync(
                _stream,
                StreamState.Any,
                new[] { eventData },
                cancellationToken: cancellationToken
            );
        }

        EventData CreateMessage(object message, Type type) {
            var msg      = Ensure.NotNull(message, nameof(message));
            var typeName = TypeMap.GetTypeNameByType(type);

            return new EventData(
                Uuid.NewUuid(),
                typeName,
                _serializer.Serialize(msg),
                null,
                _serializer.ContentType
            );
        }
    }
}