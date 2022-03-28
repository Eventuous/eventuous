using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Elasticsearch.Net;
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
    public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType)
        => new SuccessfullyDeserialized(data.ToArray());

    public SerializationResult SerializeEvent(object evt) => DefaultEventSerializer.Instance.SerializeEvent(evt);
}

class ElasticSerializer : IElasticsearchSerializer {
    readonly        IElasticsearchSerializer _builtIn;
    static readonly JsonSerializerOptions    Options = new(JsonSerializerDefaults.Web);

    public ElasticSerializer(IElasticsearchSerializer builtIn) => _builtIn = builtIn;

    public object Deserialize(Type type, Stream stream) => _builtIn.Deserialize(type, stream);

    public T Deserialize<T>(Stream stream) => _builtIn.Deserialize<T>(stream);

    public Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = new())
        => _builtIn.DeserializeAsync(type, stream, cancellationToken);

    public Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = new())
        => _builtIn.DeserializeAsync<T>(stream, cancellationToken);

    public void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None) {
        if (data is not PersistedEvent persistedEvent) {
            _builtIn.Serialize(data, stream, formatting);
            return;
        }

        var payload = persistedEvent.Message as byte[];
        var doc     = JsonSerializer.SerializeToDocument(persistedEvent with { Message = null }, Options);

        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        foreach (var jsonElement in doc.RootElement.EnumerateObject()) {
            if (jsonElement.NameEquals("message")) {
                writer.WritePropertyName("message");
                writer.WriteRawValue(payload);
            }
            else if (jsonElement.NameEquals("created")) {
                writer.WriteString("@timestamp", persistedEvent.Created);
            }
            else {
                jsonElement.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
        writer.Flush();
    }

    public Task SerializeAsync<T>(
        T                       data,
        Stream                  stream,
        SerializationFormatting formatting        = SerializationFormatting.None,
        CancellationToken       cancellationToken = new()
    ) {
        Serialize(data, stream, formatting);
        return Task.CompletedTask;
    }
}
