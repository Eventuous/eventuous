using Microsoft.Extensions.Logging;

namespace Eventuous.TestHelpers;

public static class Logging {
    public static ILoggerFactory GetLoggerFactory(ITestOutputHelper outputHelper, LogLevel logLevel = LogLevel.Debug)
        => LoggerFactory.Create(
            builder => builder
                .SetMinimumLevel(logLevel)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("Grpc.Net.Client", LogLevel.Warning)
                .AddXunit(outputHelper, logLevel)
        );
}
