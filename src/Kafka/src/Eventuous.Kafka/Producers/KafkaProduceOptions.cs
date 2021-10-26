namespace Eventuous.Kafka.Producers;

[PublicAPI]
public class KafkaProduceOptions {
    public Dictionary<string, string> Headers     { get; internal set; }
    public Type                       MessageType { get; internal set; }
    public Guid                       RequestId   { get; internal set; }
    public string?                    Encoding    { get; internal set; }
    public string                     MessageKey  { get; set; }
    public DateTimeOffset             Timestamp   { get; set; }
}