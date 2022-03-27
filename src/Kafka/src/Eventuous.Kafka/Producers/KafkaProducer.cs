using System.Text.Json;
using Confluent.Kafka;
using Eventuous.Producers;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Kafka.Producers;

/// <summary>
/// Producer for Kafka
/// </summary>
[PublicAPI]
public class KafkaProducer : BaseProducer<KafkaProduceOptions>, IHostedService {
    readonly Func<StreamName, MessageRoute> route;
    IProducer<string, object>               producer;

    public KafkaProducer(KafkaProducerOptions options) {
        producer = new ProducerBuilder<string, object>(options.Headers).Build();
        route    = stream => DefaultRouters.RouteByCategory(stream); // TODO: Router should be configurable
    }

    protected override async Task ProduceMessages(StreamName stream,
        IEnumerable<ProducedMessage>                         messages,
        KafkaProduceOptions?                                 options,
        CancellationToken                                    cancellationToken = default) {
        var (topic, partitionKey) = route(stream);
        foreach (var message in messages) {
            var kafkaMessage = new Message<string, object> {
                Key = partitionKey,
                Value = message.Message
            };

            var deliveryResult = await producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            // TODO: Use the delivery results
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        ReadyNow();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        producer.Flush(cancellationToken); // TODO: Is usage of Flush() correct here?
        producer.Dispose();
        return Task.CompletedTask;
    }
}