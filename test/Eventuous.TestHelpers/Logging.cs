using Microsoft.Extensions.Logging;

namespace Eventuous.TestHelpers;

public static class Logging {
    public static ILoggerFactory GetLoggerFactory(ITestOutputHelper outputHelper)
        => LoggerFactory.Create(
            builder => builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddXunit(outputHelper)
        );
}