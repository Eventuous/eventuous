// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SignalR;

/// <summary>
/// Wire-format envelope for a single event transmitted over SignalR. Contains the JSON-serialized payload,
/// position information, and optional trace metadata.
/// </summary>
public record StreamEventEnvelope {
    /// <summary>Unique identifier of the event.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Name of the stream the event belongs to.</summary>
    public required string Stream { get; init; }

    /// <summary>Registered event type name used for deserialization.</summary>
    public required string EventType { get; init; }

    /// <summary>Position of the event within its stream.</summary>
    public required ulong StreamPosition { get; init; }

    /// <summary>Global (log-wide) position of the event.</summary>
    public required ulong GlobalPosition { get; init; }

    /// <summary>Timestamp when the event was originally created.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>JSON-serialized event payload.</summary>
    public required string JsonPayload { get; init; }

    /// <summary>Optional JSON-serialized metadata (includes trace context when tracing is enabled).</summary>
    public string? JsonMetadata { get; init; }
}
