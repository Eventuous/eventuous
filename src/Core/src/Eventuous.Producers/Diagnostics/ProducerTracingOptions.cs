using Eventuous.Diagnostics;

namespace Eventuous.Producers.Diagnostics;

public delegate ProducerTracingOptions ConfigureProducerTracing(ProducerTracingOptions defaultOptions);

public record ProducerTracingOptions {
    public string? MessagingSystem  { get; init; }
    public string? DestinationKind  { get; init; }
    public string? ProduceOperation { get; init; }

    public KeyValuePair<string, object?>[] AllTags => new KeyValuePair<string, object?>[] {
        new(TelemetryTags.Messaging.System, MessagingSystem),
        new(TelemetryTags.Messaging.DestinationKind, DestinationKind),
        new(TelemetryTags.Messaging.Operation, ProduceOperation)
    };
}