namespace Eventuous.Kafka.Producers;

using Confluent.Kafka;

[PublicAPI]
public class KafkaProducerOptions {
    public ProducerConfig Configuration { get; set; }
    public string?        producerName  { get; set; }
    public string?        defaultTopic  { get; set; }
    
    //IMessageSerializer? defaultSerializer { get; set; }
}