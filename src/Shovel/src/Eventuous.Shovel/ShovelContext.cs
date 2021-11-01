namespace Eventuous.Shovel;

public record ShovelContext(StreamName TargetStream, object? Message, Metadata? Metadata);

[PublicAPI]
public record ShovelContext<TProduceOptions>(
    StreamName      TargetStream,
    object?         Message,
    Metadata?       Metadata,
    TProduceOptions ProduceOptions
) : ShovelContext(TargetStream, Message, Metadata);