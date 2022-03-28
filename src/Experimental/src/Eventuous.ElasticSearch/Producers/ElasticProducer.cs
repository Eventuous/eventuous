using System.Text;
using Eventuous.Producers;
using Nest;

namespace Eventuous.ElasticSearch.Producers;

public class ElasticProducer : BaseProducer<ElasticProduceOptions> {
    readonly IElasticClient _elasticClient;

    public ElasticProducer(IElasticClient elasticClient) {
        _elasticClient = elasticClient;
        ReadyNow();
    }

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        ElasticProduceOptions?       options,
        CancellationToken            cancellationToken = default
    ) {
        var documents = messages.Select(x => x.Message);
        var mode      = options?.ProduceMode ?? ProduceMode.Create;

        var bulk   = GetOp(new BulkDescriptor(stream.ToString()));
        var result = await _elasticClient.BulkAsync(bulk, cancellationToken);

        if (!result.IsValid) {
            Console.WriteLine(result.DebugInformation);
            if (result.OriginalException != null)
                throw result.OriginalException;

            throw new InvalidOperationException(result.DebugInformation);
        }

        BulkDescriptor GetOp(BulkDescriptor descriptor)
            => mode switch {
                ProduceMode.Create => descriptor.CreateMany(documents),
                ProduceMode.Index  => descriptor.IndexMany(documents),
                _                  => throw new ArgumentOutOfRangeException()
            };
    }
}

public record ElasticProduceOptions {
    public ProduceMode ProduceMode { get; init; } = ProduceMode.Create;
}

public enum ProduceMode {
    Create,
    Index
}
