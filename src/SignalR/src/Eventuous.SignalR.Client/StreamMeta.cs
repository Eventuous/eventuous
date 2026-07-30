// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SignalR.Client;

/// <summary>
/// Metadata about the stream event delivered to a typed handler.
/// </summary>
/// <param name="Stream">The stream name the event belongs to.</param>
/// <param name="Position">The event's position within the stream.</param>
/// <param name="Timestamp">The time the event was originally created.</param>
public record StreamMeta(string Stream, ulong Position, DateTime Timestamp);
