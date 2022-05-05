namespace Eventuous.Producers;

public record ProducedMessage {
    public ProducedMessage(object message, Metadata? metadata, Guid? messageId = null) {
        Message     = message;
        Metadata    = metadata;
        MessageId   = messageId ?? Guid.NewGuid();
        MessageType = TypeMap.GetTypeName(message, false);
    }

    public object               Message     { get; }
    public Metadata?            Metadata    { get; init; }
    public Guid                 MessageId   { get; }
    public string               MessageType { get; }
    public AcknowledgeProduce?  OnAck       { get; init; }
    public ReportFailedProduce? OnNack      { get; init; }

    public ValueTask Ack() => OnAck?.Invoke(this) ?? default;

    public ValueTask Nack(string message, Exception? exception) {
        if (OnNack != null) return OnNack(this, message, exception);

        throw exception ?? new InvalidOperationException(message);
    }
}
