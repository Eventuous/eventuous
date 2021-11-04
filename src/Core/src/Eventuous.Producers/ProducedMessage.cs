namespace Eventuous.Producers;

public record ProducedMessage {
    public ProducedMessage(object message, Metadata? metadata, Guid? messageId = null) {
        Message     = message;
        Metadata    = metadata;
        MessageId   = messageId ?? Guid.NewGuid();
        MessageType = TypeMap.GetTypeName(message);
    }

    public object    Message     { get; }
    public Metadata? Metadata    { get; }
    public Guid      MessageId   { get; }
    public string    MessageType { get; }
}