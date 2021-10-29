namespace Eventuous.Shovel;

public record ShovelMessage(string TargetStream, object? Message, Metadata? Metadata);

[PublicAPI]
public record ShovelMessage<TProduceOptions>(
    string          TargetStream,
    object?         Message,
    Metadata?       Metadata,
    TProduceOptions ProduceOptions
) : ShovelMessage(TargetStream, Message, Metadata);