namespace Eventuous.Shovel;

public record ShovelMessage(StreamName TargetStream, object? Message, Metadata? Metadata);

[PublicAPI]
public record ShovelMessage<TProduceOptions>(
    StreamName      TargetStream,
    object?         Message,
    Metadata?       Metadata,
    TProduceOptions ProduceOptions
) : ShovelMessage(TargetStream, Message, Metadata);