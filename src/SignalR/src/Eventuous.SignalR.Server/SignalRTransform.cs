// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;

namespace Eventuous.SignalR.Server;

/// <summary>
/// Factory for creating gateway transform functions that convert subscription context into <see cref="StreamEventEnvelope"/> messages
/// targeted at a specific SignalR client connection.
/// </summary>
public static class SignalRTransform {
    /// <summary>
    /// Creates a transform that serializes events into <see cref="StreamEventEnvelope"/> and routes them to the specified connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID of the target client.</param>
    /// <param name="stream">The stream name to include in the envelope.</param>
    /// <param name="serializer">The event serializer to use for payload serialization.</param>
    /// <returns>A <see cref="RouteAndTransform{T}"/> delegate for use with the gateway.</returns>
    public static RouteAndTransform<SignalRProduceOptions> Create(string connectionId, string stream, IEventSerializer serializer)
        => ctx => {
            var result = serializer.SerializeEvent(ctx.Message!);

            var envelope = new StreamEventEnvelope {
                EventId        = Guid.TryParse(ctx.MessageId, out var id) ? id : Guid.NewGuid(),
                Stream         = stream,
                EventType      = ctx.MessageType,
                StreamPosition = ctx.StreamPosition,
                GlobalPosition = ctx.GlobalPosition,
                Timestamp      = ctx.Created,
                JsonPayload    = Encoding.UTF8.GetString(result.Payload),
                JsonMetadata = ctx.Metadata is { Count: > 0 }
                    ? JsonSerializer.Serialize(ctx.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value))
                    : null
            };

            return ValueTask.FromResult(
                new[] {
                    new GatewayMessage<SignalRProduceOptions>(new(stream), envelope, ctx.Metadata, new(connectionId))
                }
            );
        };
}
