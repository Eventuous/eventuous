// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Microsoft.Extensions.Logging;

namespace Eventuous.TestHelpers.Logging;

public sealed class XUnitLogger<T>(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider) : XUnitLogger(testOutputHelper, scopeProvider, typeof(T).FullName), ILogger<T>;

public class XUnitLogger : ILogger {
    private readonly ITestOutputHelper           _testOutputHelper;
    private readonly string?                     _categoryName;
    private readonly XUnitLoggerOptions          _options;
    private readonly LoggerExternalScopeProvider _scopeProvider;

    public static ILogger CreateLogger(ITestOutputHelper testOutputHelper) => new XUnitLogger(testOutputHelper, new(), "");

    public static ILogger<T> CreateLogger<T>(ITestOutputHelper testOutputHelper) => new XUnitLogger<T>(testOutputHelper, new());

    public XUnitLogger(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider, string? categoryName, bool appendScope = true)
        : this(testOutputHelper, scopeProvider, categoryName, options: new() { IncludeScopes = appendScope }) { }

    public XUnitLogger(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider, string? categoryName, XUnitLoggerOptions? options) {
        _testOutputHelper = testOutputHelper;
        _scopeProvider    = scopeProvider;
        _categoryName     = categoryName;
        _options          = options ?? new();
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        var sb = new StringBuilder();

        if (_options.TimestampFormat is not null) {
            var now       = _options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
            var timestamp = now.ToString(_options.TimestampFormat);
            sb.Append(timestamp).Append(' ');
        }

        if (_options.IncludeLogLevel) {
            sb.Append(GetLogLevelString(logLevel)).Append(' ');
        }

        if (_options.IncludeCategory) {
            sb.Append('[').Append(_categoryName).Append("] ");
        }

        sb.Append(formatter(state, exception));

        if (exception is not null) {
            sb.Append('\n').Append(exception);
        }

        // Append scopes
        if (_options.IncludeScopes) {
            _scopeProvider.ForEachScope(
                (scope, s) => {
                    s.Append("\n => ");
                    s.Append(scope);
                },
                sb
            );
        }

        try {
            _testOutputHelper.WriteLine(sb.ToString());
        } catch {
            // This can happen when the test is not active
        }
    }

    private static string GetLogLevelString(LogLevel logLevel) => logLevel switch {
        LogLevel.Trace       => "trce",
        LogLevel.Debug       => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning     => "warn",
        LogLevel.Error       => "fail",
        LogLevel.Critical    => "crit",
        _                    => throw new ArgumentOutOfRangeException(nameof(logLevel))
    };
}
