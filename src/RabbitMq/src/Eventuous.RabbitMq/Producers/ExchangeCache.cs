// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;

namespace Eventuous.RabbitMq.Producers;

class ExchangeCache {
    public ExchangeCache(ILogger? log)
        => _log = log;

    public void EnsureExchange(string name, Action createExchange) {
        if (_exchanges.Contains(name)) return;

        try {
            _log?.LogInformation("Ensuring exchange {ExchangeName}", name);
            createExchange();
        }
        catch (Exception e) {
            _log?.LogError(e, "Failed to ensure exchange {ExchangeName}: {ErrorMessage}", name, e.Message);
            throw;
        }

        _exchanges.Add(name);
    }

    readonly HashSet<string> _exchanges = new();
    readonly ILogger?        _log;
}
