using Microsoft.Extensions.Logging;

namespace Eventuous.TestHelpers.Logging;

public static class LoggingExtensions {
    public static ILoggerFactory GetLoggerFactory(ITestOutputHelper outputHelper, LogLevel logLevel = LogLevel.Debug)
        => LoggerFactory.Create(
            builder => builder
                .SetMinimumLevel(logLevel)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("Grpc.Net.Client", LogLevel.Warning)
                .AddXUnit(outputHelper)
        );
    
    public static ILoggerFactory AddXUnit(this ILoggerFactory factory, ITestOutputHelper outputHelper, XUnitLoggerOptions? options = null) {
        factory.AddProvider(new XUnitLoggerProvider(outputHelper, options));

        return factory;
    }

    public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper outputHelper, XUnitLoggerOptions? options = null)
        => builder.AddProvider(new XUnitLoggerProvider(outputHelper, options));
}

public sealed class XUnitLoggerProvider(ITestOutputHelper testOutputHelper, XUnitLoggerOptions? options = null) : ILoggerProvider {
    private readonly XUnitLoggerOptions          _options       = options ?? new XUnitLoggerOptions();
    private readonly LoggerExternalScopeProvider _scopeProvider = new();

    public XUnitLoggerProvider(ITestOutputHelper testOutputHelper, bool appendScope) : this(testOutputHelper, new XUnitLoggerOptions { IncludeScopes = appendScope }) { }

    public ILogger CreateLogger(string categoryName) => new XUnitLogger(testOutputHelper, _scopeProvider, categoryName, _options);

    public void Dispose() { }
}
