// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem

namespace Eventuous.Subscriptions.Logging;

public class InternalLogger(ILogger logger, LogLevel logLevel, string subscriptionId) {
#pragma warning disable CA2254
    public void Log(string message, params object[] args) => logger.Log(logLevel, GetMessage(message), args);

    public void Log(Exception? exception, string message, params object[] args) => logger.Log(logLevel, exception, GetMessage(message), args);
#pragma warning restore CA2254

    string GetMessage(string message) => $"[{subscriptionId}] {message}";
}
