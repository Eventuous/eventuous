namespace Eventuous.Tests.EventStore.Fixtures;

public static class Logging {
    public static ILoggerFactory GetLoggerFactory(ITestOutputHelper outputHelper)
        => LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));
}