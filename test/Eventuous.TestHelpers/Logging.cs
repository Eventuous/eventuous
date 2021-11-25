using Microsoft.Extensions.Logging;

namespace Eventuous.TestHelpers;

public static class Logging {
    public static ILoggerFactory GetLoggerFactory(ITestOutputHelper outputHelper, LogLevel logLevel = LogLevel.Debug)
        => LoggerFactory.Create(
            builder => builder
                .SetMinimumLevel(logLevel)
                .AddXunit(outputHelper, logLevel)
        );
}