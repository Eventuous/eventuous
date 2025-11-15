using Eventuous.KurrentDB;
using Eventuous.KurrentDB.Producers;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Tests.OpenTelemetry.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.KurrentDB.Metrics;

public class MetricsFixture : MetricsSubscriptionFixtureBase<EventStoreDbContainer, KurrentDBProducer, StreamSubscription, StreamSubscriptionOptions> {
    protected override EventStoreDbContainer CreateContainer() => EsdbContainer.Create();

    protected override void ConfigureSubscription(StreamSubscriptionOptions options) => options.StreamName = Stream;

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddKurrentDBClient(Container.GetConnectionString());
        services.AddEventStore<KurrentDBEventStore>();
    }
}
