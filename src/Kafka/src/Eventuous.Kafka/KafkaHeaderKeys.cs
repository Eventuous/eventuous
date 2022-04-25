namespace Eventuous.Kafka;

public static class KafkaHeaderKeys {
    public static string MessageTypeHeader { get; set; } = "message-type";
    public static string ContentTypeHeader { get; set; } = "content-type";
}
