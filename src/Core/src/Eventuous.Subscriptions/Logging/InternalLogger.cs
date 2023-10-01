// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using System.Collections.Immutable;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem

namespace Eventuous.Subscriptions.Logging;

public class InternalLogger(ILogger logger, LogLevel logLevel, string subscriptionId) {
    readonly ImmutableDictionary<string, object> _scope = ImmutableDictionary<string, object>.Empty.Add("SubscriptionId", subscriptionId);

#pragma warning disable CA2254
    public void Log(string message, params object[] args) {
        using (logger.BeginScope(_scope)) {
            logger.Log(logLevel, GetMessage(message), args);
        }
    }

    public void Log(Exception? exception, string message, params object[] args) {
        using (logger.BeginScope(_scope)) {
            logger.Log(logLevel, exception, GetMessage(message), args);
        }
    }
#pragma warning restore CA2254

    string GetMessage(string message) => $"[{subscriptionId}] {message}";
}
