namespace Eventuous.Producers;

public record ProducedMessage(object Message, Metadata? Metadata);