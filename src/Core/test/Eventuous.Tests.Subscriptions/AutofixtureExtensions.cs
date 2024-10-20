using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers.Logging;

namespace Eventuous.Tests.Subscriptions;

public static class AutoFixtureExtensions {
    public static MessageConsumeContext CreateContext(this Fixture auto, ITestOutputHelper output) {
        var factory = new LoggerFactory().AddXUnit(output);
        return auto.Build<MessageConsumeContext>().With(x => x.LogContext, () => new("test", factory)).Create();
    }
}
