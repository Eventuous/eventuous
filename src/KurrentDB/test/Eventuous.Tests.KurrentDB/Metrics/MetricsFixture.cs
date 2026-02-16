using Eventuous.KurrentDB;
using Eventuous.KurrentDB.Producers;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Tests.OpenTelemetry.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.KurrentDb;

namespace Eventuous.Tests.KurrentDB.Metrics;

public class MetricsFixture : MetricsSubscriptionFixtureBase<KurrentDbContainer, KurrentDBProducer, StreamSubscription, StreamSubscriptionOptions> {
    protected override KurrentDbContainer CreateContainer() => KurrentDBContainer.Create();

    protected override void ConfigureSubscription(StreamSubscriptionOptions options) => options.StreamName = Stream;

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddKurrentDBClient(Container.GetConnectionString());
        services.AddEventStore<KurrentDBEventStore>();
    }
}
