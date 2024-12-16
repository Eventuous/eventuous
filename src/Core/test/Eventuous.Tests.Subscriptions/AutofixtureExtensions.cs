using Bogus;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers.TUnit.Logging;

namespace Eventuous.Tests.Subscriptions;

public static class TestContext {
    static readonly Faker<MessageConsumeContext> Auto = new Faker<MessageConsumeContext>()
        .RuleFor(x => x.LogContext, (f, c) => new("test", new LoggerFactory().AddTUnit()))
        .RuleFor(x => x.MessageId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.MessageType, f => f.Random.Guid().ToString());
    
    public static MessageConsumeContext CreateContext() => Auto.Generate();
}
