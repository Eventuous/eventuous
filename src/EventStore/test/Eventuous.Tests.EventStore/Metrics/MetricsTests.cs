using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.OpenTelemetry;
using Testcontainers.EventStoreDb;
// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.EventStore.Metrics;

public class MetricsTests : MetricsTestsBase<MetricsFixture, EventStoreDbContainer, EventStoreProducer, StreamSubscription, StreamSubscriptionOptions> {
    [Test]
    public async Task ShouldMeasureSubscriptionGapCount() {
        await ShouldMeasureSubscriptionGapCountBase();
    }

    [Before(Test)]
    public async Task Setup() => await InitializeAsync();
    
    [After(Test)]
    public async Task TearDown() => await DisposeAsync();
}
