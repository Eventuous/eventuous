using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Eventuous.TestHelpers.TUnit.Logging;

public class TUnitLog(LoggerExternalScopeProvider scopeProvider, string category, LogLevel logLevel) : ILogger {
    public bool IsEnabled(LogLevel level) => level >= logLevel;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => scopeProvider.Push(state);

    public void Log<TState>(LogLevel l, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (TestContext.Current == null || !IsEnabled(l)) return;

        var level = l switch {
            LogLevel.Trace       => global::TUnit.Core.Logging.LogLevel.Trace,
            LogLevel.Debug       => global::TUnit.Core.Logging.LogLevel.Debug,
            LogLevel.Information => global::TUnit.Core.Logging.LogLevel.Information,
            LogLevel.Warning     => global::TUnit.Core.Logging.LogLevel.Warning,
            LogLevel.Error       => global::TUnit.Core.Logging.LogLevel.Error,
            LogLevel.Critical    => global::TUnit.Core.Logging.LogLevel.Critical,
            _                    => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
        TestContext.Current.OutputWriter.WriteLine($"[{category}] {level}: {formatter(state, exception)}");
    }
}
