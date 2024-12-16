using Bogus;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers.TUnit.Logging;

namespace Eventuous.Tests.Subscriptions;

public static class TestContext {
    static readonly Faker<MessageConsumeContext> Auto = new Faker<MessageConsumeContext>()
        .CustomInstantiator(
            f => new(
                f.Random.String(),
                f.Random.String(),
                f.Random.String(),
                f.Random.String(),
                f.Random.ULong(),
                f.Random.ULong(),
                f.Random.ULong(),
                f.Random.ULong(),
                f.Date.Past(),
                new(),
                null,
                f.Random.String(),
                CancellationToken.None
            )
        )
        .RuleFor(x => x.LogContext, (_, _) => new("test", new LoggerFactory().AddTUnit()));
    
    public static MessageConsumeContext CreateContext() => Auto.Generate();
}
