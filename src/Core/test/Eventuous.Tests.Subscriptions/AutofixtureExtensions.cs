using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers.TUnit.Logging;

namespace Eventuous.Tests.Subscriptions;

public static class AutoFixtureExtensions {
    public static MessageConsumeContext CreateContext(this Fixture auto) {
        var factory = new LoggerFactory().AddTUnit();
        return auto.Build<MessageConsumeContext>().With(x => x.LogContext, () => new("test", factory)).Create();
    }
}
