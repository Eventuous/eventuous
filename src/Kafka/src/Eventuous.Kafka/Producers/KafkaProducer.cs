using System.Text.Json;
using Confluent.Kafka;
using Eventuous.Producers;

namespace Eventuous.Kafka.Producers;

/// <summary>
/// Producer for Kafka
/// </summary>
[PublicAPI]
public class KafkaProducer : BaseProducer<KafkaProducerOptions> {
    protected override async Task ProduceMessages(StreamName stream,
        IEnumerable<ProducedMessage>                         messages,
        KafkaProducerOptions?                                options,
        CancellationToken                                    cancellationToken = default) {
        var properties = new Dictionary<string, string> {
            { "bootstrap.servers", "pkc-41mxj.uksouth.azure.confluent.cloud:9092" },
            { "security.protocol", "SASL_SSL" },
            { "sasl.mechanisms", "PLAIN" },
            { "sasl.username", "UZ7LAF7S5FBDPB6Q" },
            { "sasl.password", "Mh5h98gVO5tZ6xAi4eAtRT4V8sA1Mt9lT7bOak0Vep/GRLTsYUs3VLHeVF4mwS+v" },
            { "retries", "3" }
        };

        var config = new ClientConfig(properties); // TODO: How will I populate the properties?
        var producerConfig = new ProducerConfig(); // TODO: How will I populate producer config properties?
        
        using var producer = new ProducerBuilder<string, string>(config).Build();
        foreach (var message in messages) {
            var kafkaMessage = new Message<string, string> {
                // Key   = "", // TODO: What can I use as a partition key
                Value = JsonSerializer.Serialize(message.Message) // TODO: I presume that we serialize the message as json for the Kafka message body.
            };

           var deliveryResult = await producer.ProduceAsync(stream, kafkaMessage, cancellationToken);
           // TODO: Use the delivery results
        }
    }
}