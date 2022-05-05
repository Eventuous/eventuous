using Eventuous.Producers;
using Eventuous.Subscriptions.Diagnostics;
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
        var messagesList = messages.ToList();
        var documents    = messagesList.Select(x => x.Message);
        var mode         = options?.ProduceMode ?? ProduceMode.Create;

        var bulk   = GetOp(new BulkDescriptor(stream.ToString()));
        var result = await _elasticClient.BulkAsync(bulk, cancellationToken);

        if (!result.IsValid) {
            if (result.DebugInformation.Contains("version conflict")) {
                SubscriptionsEventSource.Log.Warn("ElasticProducer: version conflict");
            }
            else {
                var errors = messagesList
                    .Where(x => result.ItemsWithErrors.Any(y => y.Id == x.MessageId.ToString()))
                    .ToList();

                foreach (var error in errors) {
                    await error.Nack(result.DebugInformation, result.OriginalException).NoContext();
                }

                messagesList = messagesList.Except(errors).ToList();
            }
        }

        await Task.WhenAll(messagesList.Select(x => x.Ack().AsTask())).NoContext();

        BulkDescriptor GetOp(BulkDescriptor descriptor)
            => mode switch {
                ProduceMode.Create => descriptor.CreateMany<object>(
                    messagesList,
                    (createDescriptor, o) => {
                        var pm = o as ProducedMessage;
                        return createDescriptor.Document(pm!.Message).Id(pm.MessageId);
                    }
                ),
                ProduceMode.Index => descriptor.IndexMany(documents),
                _                 => throw new ArgumentOutOfRangeException()
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
