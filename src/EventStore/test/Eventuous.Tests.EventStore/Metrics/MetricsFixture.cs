using Eventuous.EventStore;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.OpenTelemetry.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Metrics;

public class MetricsFixture : MetricsSubscriptionFixtureBase<EventStoreDbContainer, EventStoreProducer, StreamSubscription, StreamSubscriptionOptions> {
    protected override EventStoreDbContainer CreateContainer() => EsdbContainer.Create();

    protected override void ConfigureSubscription(StreamSubscriptionOptions options) => options.StreamName = Stream;

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddEventStoreClient(Container.GetConnectionString());
        services.AddEventStore<EsdbEventStore>();
    }
}
