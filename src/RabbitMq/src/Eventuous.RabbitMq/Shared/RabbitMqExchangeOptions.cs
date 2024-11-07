// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.RabbitMq.Shared;

public class RabbitMqExchangeOptions {
    public string Type       { get; init; } = ExchangeType.Fanout;
    public bool   Durable    { get; init; } = true;
    public bool   AutoDelete { get; init; }

    public IDictionary<string, object>? Arguments { get; init; }
}