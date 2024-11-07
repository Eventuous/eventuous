// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;

namespace Eventuous.RabbitMq.Producers;

class ExchangeCache(ILogger? log) {
    public void EnsureExchange(string name, Action createExchange) {
        if (_exchanges.Contains(name)) return;

        try {
            log?.LogInformation("Ensuring exchange {ExchangeName}", name);
            createExchange();
        }
        catch (Exception e) {
            log?.LogError(e, "Failed to ensure exchange {ExchangeName}: {ErrorMessage}", name, e.Message);
            throw;
        }

        _exchanges.Add(name);
    }

    readonly HashSet<string> _exchanges = [];
}
