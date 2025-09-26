using AutoFixture;
using Bogus;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers.TUnit.Logging;
using MicroElements.AutoFixture.NodaTime;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public static class TestContext<T> where T : class {
    // ReSharper disable once StaticMemberInGenericType
    static readonly IFixture Fixture = new Fixture().Customize(new NodaTimeCustomization());
    
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
                Fixture.Create<T>(),
                null,
                f.Random.String(),
                CancellationToken.None
            )
        )
        .RuleFor(x => x.LogContext, (_, _) => new("test", new LoggerFactory().AddTUnit(LogLevel.Information)));
    
    public static MessageConsumeContext CreateContext() => Auto.Generate();
}
