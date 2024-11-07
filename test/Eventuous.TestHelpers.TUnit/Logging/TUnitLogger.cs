using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Eventuous.TestHelpers.TUnit.Logging;

public class TUnitLog(LoggerExternalScopeProvider scopeProvider) : ILogger {
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => scopeProvider.Push(state);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (TestContext.Current == null) return;

        var level = logLevel switch {
            LogLevel.Trace       => global::TUnit.Core.Logging.LogLevel.Trace,
            LogLevel.Debug       => global::TUnit.Core.Logging.LogLevel.Debug,
            LogLevel.Information => global::TUnit.Core.Logging.LogLevel.Information,
            LogLevel.Warning     => global::TUnit.Core.Logging.LogLevel.Warning,
            LogLevel.Error       => global::TUnit.Core.Logging.LogLevel.Error,
            LogLevel.Critical    => global::TUnit.Core.Logging.LogLevel.Critical,
            _                    => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
        TestContext.Current.GetDefaultLogger().Log(level, state, exception, formatter);
    }
}
