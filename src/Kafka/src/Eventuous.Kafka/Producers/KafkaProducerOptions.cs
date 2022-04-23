using Confluent.Kafka;

namespace Eventuous.Kafka.Producers; 

public record KafkaProducerOptions(ProducerConfig ProducerConfig);
