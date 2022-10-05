// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.Logging;

public class InternalLogger {
    readonly ILogger  _logger;
    readonly LogLevel _logLevel;
    readonly string   _subscriptionId;

    public InternalLogger(ILogger logger, LogLevel logLevel, string subscriptionId) {
        _logger         = logger;
        _logLevel       = logLevel;
        _subscriptionId = subscriptionId;
    }

#pragma warning disable CA2254
    public void Log(string message, params object[] args) => _logger.Log(_logLevel, GetMessage(message), args);

    public void Log(Exception? exception, string message, params object[] args)
        => _logger.Log(_logLevel, exception, GetMessage(message), args);
#pragma warning restore CA2254
    
    string GetMessage(string message) => $"[{_subscriptionId}] {message}";
}
