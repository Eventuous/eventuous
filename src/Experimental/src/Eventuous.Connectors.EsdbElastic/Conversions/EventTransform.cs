using Eventuous.ElasticSearch.Producers;
using Eventuous.Gateway;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Connectors.EsdbElastic.Conversions;

public class EventTransform : IGatewayTransform<ElasticProduceOptions> {
    readonly string _indexName;

    static readonly ElasticProduceOptions Options = new() { ProduceMode = ProduceMode.Create };

    public EventTransform(string indexName) => _indexName = indexName;

    public ValueTask<GatewayContext<ElasticProduceOptions>?> RouteAndTransform(IMessageConsumeContext context) {
        var ctx = new GatewayContext<ElasticProduceOptions>(
            new StreamName(_indexName),
            PersistedEvent.From(context),
            null,
            Options
        );

        return new ValueTask<GatewayContext<ElasticProduceOptions>?>(ctx);
    }
}
