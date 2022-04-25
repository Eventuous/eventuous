namespace Eventuous.Gateway;

public record GatewayMessage(StreamName TargetStream, object Message, Metadata? Metadata);

[PublicAPI]
public record GatewayMessage<TProduceOptions>(
    StreamName      TargetStream,
    object          Message,
    Metadata?       Metadata,
    TProduceOptions ProduceOptions
) : GatewayMessage(TargetStream, Message, Metadata);
