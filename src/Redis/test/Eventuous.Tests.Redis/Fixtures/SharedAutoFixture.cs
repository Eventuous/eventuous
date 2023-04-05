using MicroElements.AutoFixture.NodaTime;

namespace Eventuous.Tests.Redis.Fixtures;

public static class SharedAutoFixture {
    public static IFixture Auto { get; } = new Fixture().Customize(new NodaTimeCustomization());
}
