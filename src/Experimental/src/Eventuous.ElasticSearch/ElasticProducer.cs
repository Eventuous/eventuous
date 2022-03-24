using Eventuous.Producers;
using Nest;

namespace Eventuous.ElasticSearch; 

public class ElasticProducer : BaseProducer<ElasticProduceOptions> {
    public IElasticClient _elasticClient;

    public ElasticProducer(IElasticClient elasticClient) {
        _elasticClient = elasticClient;
    }

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        ElasticProduceOptions?       options,
        CancellationToken            cancellationToken = default
    ) {
        // var result      = await _elasticClient.CreateAsync(message, idx => idx.Index(StreamName), ct);

        // if (!result.IsValid)
            // Log?.LogError(result.OriginalException, "Error indexing {Message}", message);
    }
}

public record ElasticProduceOptions {}