// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Eventuous.RabbitMq.Producers;

public class RabbitMqProduceOptions {
    public string? RoutingKey   { get; init; }
    public string? AppId        { get; init; }

    /// <summary>
    /// Message time-to-live in milliseconds
    /// </summary>
    public int?    Expiration   { get; init; }
    public byte    Priority     { get; init; }
    public string? ReplyTo      { get; init; }
    public bool    Persisted    { get; init; } = true;
}
