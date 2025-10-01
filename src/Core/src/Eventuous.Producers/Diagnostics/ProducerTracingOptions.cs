// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;

namespace Eventuous.Producers.Diagnostics;

public record ProducerTracingOptions {
    public string? MessagingSystem  { get; init; }
    public string? DestinationKind  { get; init; }
    public string? ProduceOperation { get; init; }

    public KeyValuePair<string, object?>[] AllTags => [
        new(TelemetryTags.Messaging.System, MessagingSystem),
        new(TelemetryTags.Messaging.DestinationKind, DestinationKind),
        new(TelemetryTags.Messaging.Operation, ProduceOperation)
    ];
}