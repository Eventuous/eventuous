// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Eventuous.RabbitMq.Producers;

class ExchangeCache(ILogger? log) {
    public async Task EnsureExchange(string name, Func<Task> createExchange) {
        if (_exchanges.ContainsKey(name)) return;

        try {
            log?.LogInformation("Ensuring exchange {ExchangeName}", name);
            await createExchange().NoContext();
        }
        catch (Exception e) {
            log?.LogError(e, "Failed to ensure exchange {ExchangeName}: {ErrorMessage}", name, e.Message);
            throw;
        }

        // Concurrent producers can race here and declare the same exchange more than once. The
        // declaration is idempotent on the broker (identical arguments), so the race only costs an
        // extra round trip, while a failed declaration stays out of the cache and is retried.
        _exchanges.TryAdd(name, true);
    }

    readonly ConcurrentDictionary<string, bool> _exchanges = new();
}
