namespace Eventuous.Shovel;

public record ShovelMessage(string TargetStream, object? Message);

[PublicAPI]
public record ShovelMessage<TProduceOptions>(string TargetStream, object? Message, TProduceOptions ProduceOptions);