using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscriptionTestBase(IStartableFixture fixture) {
    [Before(Test)]
    public async Task Startup() {
        await fixture.InitializeAsync();
    }

    [After(Test)]
    public async Task Shutdown() {
        await fixture.DisposeAsync();
    }
}
