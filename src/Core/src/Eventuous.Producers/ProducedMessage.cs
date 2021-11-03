namespace Eventuous.Producers;

public record ProducedMessage {
    public ProducedMessage(object message, Metadata? metadata, Guid? messageId = null) {
        Message   = message;
        Metadata  = metadata;
        MessageId = messageId ?? Guid.NewGuid();
    }

    public object    Message   { get; }
    public Metadata? Metadata  { get; }
    public Guid      MessageId { get; }
}