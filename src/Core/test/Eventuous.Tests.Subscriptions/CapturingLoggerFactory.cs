using System.Collections.Concurrent;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Captures log lines so a test can assert on what the production code reported. Defaults to warnings and
/// above — the level most assertions want — but takes anything down to <see cref="LogLevel.Trace"/> for the
/// tests that assert on the supervisor's own debug narration ("Resubscribing", "belongs to a previous run").
/// </summary>
/// <remarks>
/// The exception is appended to the captured text: <c>ILogger</c>'s formatter only renders the state, so
/// text that exists solely in the exception message (e.g. "CancellationTokenSource has been disposed")
/// would otherwise never match.
/// </remarks>
sealed class CapturingLoggerFactory(LogLevel minimum = LogLevel.Warning) : ILoggerFactory {
    readonly ConcurrentQueue<string> _lines = [];

    /// <summary>
    /// Polls rather than waiting out the full timeout, checking once more after the deadline for a
    /// boundary-line match.
    /// </summary>
    public Task<bool> WaitForWarning(string contains, TimeSpan timeout) => Wait.Until(() => Contains(contains), timeout);

    public bool Contains(string text) => _lines.Any(line => line.Contains(text));

    public int Count(string text) => _lines.Count(line => line.Contains(text));

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines, minimum);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    sealed class CapturingLogger(ConcurrentQueue<string> lines, LogLevel minimum) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (logLevel < minimum) return;

            var line = formatter(state, exception);

            lines.Enqueue(exception is null ? line : $"{line} {exception}");
        }
    }
}
