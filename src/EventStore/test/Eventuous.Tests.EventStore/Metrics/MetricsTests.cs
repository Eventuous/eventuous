using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.OpenTelemetry;
using Testcontainers.EventStoreDb;
// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.EventStore.Metrics;

public class MetricsTests(MetricsFixture fixture, ITestOutputHelper outputHelper)
    : MetricsTestsBase<MetricsFixture, EventStoreDbContainer, EventStoreProducer, StreamSubscription, StreamSubscriptionOptions>(fixture, outputHelper) { }
