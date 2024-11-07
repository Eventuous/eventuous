// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;

namespace Eventuous.Producers.Diagnostics;

public delegate ProducerTracingOptions ConfigureProducerTracing(ProducerTracingOptions defaultOptions);

public record ProducerTracingOptions {
    public string? MessagingSystem  { get; init; }
    public string? DestinationKind  { get; init; }
    public string? ProduceOperation { get; init; }

    public KeyValuePair<string, object?>[] AllTags => [
        new KeyValuePair<string, object?>(TelemetryTags.Messaging.System, MessagingSystem),
        new KeyValuePair<string, object?>(TelemetryTags.Messaging.DestinationKind, DestinationKind),
        new KeyValuePair<string, object?>(TelemetryTags.Messaging.Operation, ProduceOperation)
    ];
}