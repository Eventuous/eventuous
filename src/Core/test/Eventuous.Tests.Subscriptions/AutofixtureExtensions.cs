using Eventuous.Subscriptions.Context;

namespace Eventuous.Tests.Subscriptions;

public static class AutoFixtureExtensions {
    public static MessageConsumeContext CreateContext(this Fixture auto, ITestOutputHelper output) {
        var factory = new LoggerFactory().AddXunit(output, LogLevel.Trace);
        return auto.Build<MessageConsumeContext>().With(x => x.LogContext, () => new("test", factory)).Create();
    }
}
