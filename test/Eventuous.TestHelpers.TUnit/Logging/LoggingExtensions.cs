using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Eventuous.TestHelpers.TUnit.Logging;

public static class LoggingExtensions {
    public static ILoggerFactory GetLoggerFactory(LogLevel logLevel = LogLevel.Information)
        => LoggerFactory.Create(builder => builder
            .SetMinimumLevel(logLevel)
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("Grpc", LogLevel.Warning)
            .AddTUnit(logLevel)
        );

    public static ILoggerFactory AddTUnit(this ILoggerFactory factory, LogLevel logLevel) {
        factory.AddProvider(new TUnitLoggerProvider(logLevel));

        return factory;
    }

    extension(ILoggingBuilder builder) {
        public ILoggingBuilder AddTUnit(LogLevel logLevel) => builder.AddProvider(new TUnitLoggerProvider(logLevel));

        public ILoggingBuilder ForTests(LogLevel logLevel = LogLevel.Information)
            => builder
                .AddTUnit(logLevel)
                .SetMinimumLevel(logLevel)
                .AddFilter("Grpc", LogLevel.Warning)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("Npgsql", LogLevel.Warning);
    }
}

public sealed class TUnitLoggerProvider(LogLevel logLevel) : ILoggerProvider {
    private readonly LoggerExternalScopeProvider            _scopeProvider = new();
    private readonly ConcurrentDictionary<string, TUnitLog> _loggers       = new(StringComparer.OrdinalIgnoreCase);

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new(_scopeProvider, name, logLevel));

    public void Dispose() => _loggers.Clear();
}
