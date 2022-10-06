// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eventuous.Subscriptions.Logging;

public static class Logger {
    static readonly AsyncLocal<LogContext> Context = new();

    public static LogContext Current {
        get => Context.Value!;
        set {
            if (Context.Value != value) Context.Value = value;
        }
    }

    public static void ConfigureIfNull(string subscriptionId, ILoggerFactory? loggerFactory = null)
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        => Current ??= CreateContext(subscriptionId, loggerFactory);

    public static void Configure(string subscriptionId, ILoggerFactory? loggerFactory = null) {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        if (Current?.SubscriptionId == subscriptionId) return;

        Current = CreateContext(subscriptionId, loggerFactory);
    }

    public static LogContext CreateContext(string subscriptionId, ILoggerFactory? loggerFactory)
        => new(subscriptionId, loggerFactory ?? NullLoggerFactory.Instance);
}

[SuppressMessage("Usage", "CA2254:Template should be a static expression")]
public class LogContext {
    internal string  SubscriptionId { get; }
    readonly ILogger _logger;

    public InternalLogger? TraceLog { get; }
    public InternalLogger? DebugLog { get; }
    public InternalLogger? InfoLog  { get; }
    public InternalLogger? WarnLog  { get; }
    public InternalLogger? ErrorLog { get; }

    public LogContext(string subscriptionId, ILoggerFactory loggerFactory) {
        SubscriptionId = subscriptionId;
        _logger        = loggerFactory.CreateLogger("Eventuous.Subscription");
        TraceLog       = GetLogger(LogLevel.Trace);
        DebugLog       = GetLogger(LogLevel.Debug);
        InfoLog        = GetLogger(LogLevel.Information);
        WarnLog        = GetLogger(LogLevel.Warning);
        ErrorLog       = GetLogger(LogLevel.Error);

        InternalLogger? GetLogger(LogLevel logLevel)
            => _logger.IsEnabled(logLevel) ? new InternalLogger(_logger, logLevel, SubscriptionId) : null;
    }
}
