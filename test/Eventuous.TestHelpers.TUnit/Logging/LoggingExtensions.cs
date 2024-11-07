using Microsoft.Extensions.Logging;

namespace Eventuous.TestHelpers.TUnit.Logging;

public static class LoggingExtensions {
    public static ILoggerFactory GetLoggerFactory(LogLevel logLevel = LogLevel.Debug)
        => LoggerFactory.Create(
            builder => builder
                .SetMinimumLevel(logLevel)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("Grpc", LogLevel.Warning)
                .AddTUnit()
        );
    
    public static ILoggerFactory AddTUnit(this ILoggerFactory factory) {
        factory.AddProvider(new TUnitLoggerProvider());

        return factory;
    }

    public static ILoggingBuilder AddTUnit(this ILoggingBuilder builder) => builder.AddProvider(new TUnitLoggerProvider());
    
    public static ILoggingBuilder ForTests(this ILoggingBuilder builder, LogLevel logLevel = LogLevel.Debug) 
        => builder.AddTUnit().SetMinimumLevel(logLevel).AddFilter("Grpc", LogLevel.Warning).AddFilter("Microsoft", LogLevel.Warning);
}

public sealed class TUnitLoggerProvider() : ILoggerProvider {
    private readonly LoggerExternalScopeProvider _scopeProvider = new();

    public ILogger CreateLogger(string categoryName) => new TUnitLog(_scopeProvider, categoryName);

    public void Dispose() { }
}
