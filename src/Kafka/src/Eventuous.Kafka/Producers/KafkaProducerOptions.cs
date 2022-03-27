namespace Eventuous.Kafka.Producers;

[PublicAPI]
public class KafkaProducerOptions {
    public Dictionary<string, string> Headers { get; internal set; }
}