namespace Eventuous.Kafka.Producers;

public static class HeaderKeys {
    // TODO SV: msg type just for backwards compatibility. remove later.
    public const string MessageType     = "streams-message-type";
    public const string RequestId       = "streams-request-id";
    public const string ProducerName    = "streams-producer";
    public const string MessageKey      = "streams-message-key";
    public const string MessageEncoding = "streams-message-encoding";
}