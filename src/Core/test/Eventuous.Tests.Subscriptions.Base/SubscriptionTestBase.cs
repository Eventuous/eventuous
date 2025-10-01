using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscriptionTestBase(IStartableFixture fixture) {
    [Before(Test)]
    public async Task Startup() {
        WriteLine("Starting the fixture");
        await fixture.InitializeAsync();
        WriteLine("Fixture started");
    }

    [After(Test)]
    public async Task Shutdown() {
        WriteLine("Stopping the fixture");
        await fixture.DisposeAsync();
        WriteLine("Fixture stopped");
    }

    protected static void WriteLine(string message) => TestContext.Current?.OutputWriter.WriteLine(message);

    protected static void WriteLine(string message, params object?[] args) => TestContext.Current?.OutputWriter.WriteLine(message, args);
}
