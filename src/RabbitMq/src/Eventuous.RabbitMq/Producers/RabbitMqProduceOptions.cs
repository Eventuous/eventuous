// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Eventuous.RabbitMq.Producers;

/// <summary>
/// Options for producing a message to RabbitMQ
/// </summary>
public class RabbitMqProduceOptions {
    /// <summary>
    /// Optional routing key that only works with direct and topic exchanges
    /// </summary>
    public string? RoutingKey { get; init; }

    /// <summary>
    /// Optional application name that helps to identify consumers in RabbitMQ management UI and API
    /// </summary>
    public string? AppId { get; init; }

    /// <summary>
    /// Message time-to-live in milliseconds
    /// </summary>
    public int? Expiration { get; init; }

    /// <summary>
    /// Message priority from 0 to 9
    /// </summary>
    public byte Priority { get; init; }

    /// <summary>
    /// Optional reply address. Eventuous doesn't support replies, so you'd normally not use this property
    /// </summary>
    public string? ReplyTo { get; init; }

    /// <summary>
    /// Whether the message persisted or not. Leave it as <code>true</code> default value for durable messaging.
    /// </summary>
    public bool Persisted { get; init; } = true;
}
