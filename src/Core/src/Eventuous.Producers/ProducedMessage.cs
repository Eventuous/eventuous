using Eventuous.Producers.Diagnostics;

namespace Eventuous.Producers;

public record ProducedMessage {
    public ProducedMessage(
        object    message,
        Metadata? metadata,
        Metadata? additionalHeaders = null,
        Guid?     messageId         = null
    ) {
        Message           = message;
        Metadata          = metadata;
        AdditionalHeaders = additionalHeaders;
        MessageId         = messageId ?? Guid.NewGuid();
        MessageType       = TypeMap.GetTypeName(message, false);
    }

    public object               Message           { get; }
    public Metadata?            Metadata          { get; init; }
    public Metadata?            AdditionalHeaders { get; }
    public Guid                 MessageId         { get; }
    public string               MessageType       { get; }
    public AcknowledgeProduce?  OnAck             { get; init; }
    public ReportFailedProduce? OnNack            { get; init; }

    public ValueTask Ack<T>() where T : IEventProducer {
        ProducerEventSource<T>.Log.ProduceAcknowledged(this);
        return OnAck?.Invoke(this) ?? default;
    }

    public ValueTask Nack<T>(string message, Exception? exception) where T : IEventProducer {
        ProducerEventSource<T>.Log.ProduceNotAcknowledged(this, message, exception);
        if (OnNack != null) return OnNack(this, message, exception);

        throw exception ?? new InvalidOperationException(message);
    }
}
