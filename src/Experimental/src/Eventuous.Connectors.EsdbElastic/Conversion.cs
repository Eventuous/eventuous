using System.Text.Json;
using Eventuous.ElasticSearch.Producers;
using Eventuous.Gateway;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Connectors.EsdbElastic; 

public class EventTransform : IGatewayTransform<ElasticProduceOptions> {
    readonly string                _indexName;
    readonly ElasticProduceOptions _options;

    public EventTransform(string indexName) {
        _indexName = indexName;
        _options   = new ElasticProduceOptions { ProduceMode = ProduceMode.Create };
    }
    
    public ValueTask<GatewayContext<ElasticProduceOptions>?> RouteAndTransform(IMessageConsumeContext context) {
        var ctx = new GatewayContext<ElasticProduceOptions>(
            new StreamName(_indexName),
            PersistedEvent.From(context),
            null,
            _options
        );

        return new ValueTask<GatewayContext<ElasticProduceOptions>?>(ctx);
    }
}

class StringSerializer : IEventSerializer {
    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
        return new SuccessfullyDeserialized(JsonSerializer.Deserialize<dynamic>(data));
    }

    public SerializationResult SerializeEvent(object evt) => DefaultEventSerializer.Instance.SerializeEvent(evt);
}