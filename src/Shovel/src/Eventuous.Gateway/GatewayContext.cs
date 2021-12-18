namespace Eventuous.Gateway;

public record GatewayContext(StreamName TargetStream, object? Message, Metadata? Metadata);

[PublicAPI]
public record GatewayContext<TProduceOptions>(
    StreamName      TargetStream,
    object?         Message,
    Metadata?       Metadata,
    TProduceOptions ProduceOptions
) : GatewayContext(TargetStream, Message, Metadata);