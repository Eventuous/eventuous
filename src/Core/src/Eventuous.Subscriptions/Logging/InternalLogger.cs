// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.Logging;

public class InternalLogger {
    readonly ILogger  _logger;
    readonly LogLevel _logLevel;

    public InternalLogger(ILogger logger, LogLevel logLevel) {
        _logger   = logger;
        _logLevel = logLevel;
    }

#pragma warning disable CA2254
    public void Log(string message, params object[] args) => _logger.Log(_logLevel, message, args);

    public void Log(Exception? exception, string message, params object[] args)
        => _logger.Log(_logLevel, exception, message, args);
#pragma warning restore CA2254
}
