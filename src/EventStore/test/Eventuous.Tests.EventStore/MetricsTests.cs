using Eventuous.Diagnostics.OpenTelemetry.Subscriptions;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Sut.Subs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Eventuous.Tests.EventStore;

public class MetricsTests : IAsyncLifetime {
    static MetricsTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    const string SubscriptionId = "test-sub";

    StreamName Stream { get; } = new($"test-{Guid.NewGuid():N}");

    public MetricsTests(ITestOutputHelper outputHelper) {
        _exporter = new TestExporter();
        var builder = new WebHostBuilder()
            .Configure(_ => { })
            .ConfigureServices(
                services => {
                    services.AddSingleton(IntegrationFixture.Instance.Client);
                    services.AddEventProducer<EventStoreProducer>();

                    services.AddCheckpointStore<NoOpCheckpointStore>();
                    services.AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
                        SubscriptionId,
                        options => options.StreamName = Stream
                    ).AddEventHandler<TestHandler>();

                    services.AddOpenTelemetryMetrics(
                        builder => {
                            builder.AddEventuousSubscriptions();
                            builder.AddReader(new BaseExportingMetricReader(_exporter));
                        }
                    );
                }
            ).ConfigureLogging(cfg => cfg.AddXunit(outputHelper));

        _host = new TestServer(builder);
    }

    [Fact]
    public async Task ShouldMeasureSubscription() {
        const int count = 1000;

        var testEvents = IntegrationFixture.Instance.Auto.CreateMany<TestEvent>(count).ToList();
        var producer = _host.Services.GetRequiredService<IEventProducer>();
        await producer.Produce(Stream, testEvents);

        _exporter.Collect(Timeout.Infinite).Should().BeTrue();
        _exporter.Batch.Should().NotBeNull();
    }
    
    readonly TestServer   _host;
    readonly TestExporter _exporter;

    public Task InitializeAsync() => _host.Host.StartAsync();

    public Task DisposeAsync() => _host.Host.StopAsync();

    class TestHandler : IEventHandler {
        public Task HandleEvent(IMessageConsumeContext context, CancellationToken cancellationToken) {
            return Task.Delay(100, cancellationToken);
        }
    }

    [ExportModes(ExportModes.Pull)]
    class TestExporter : BaseExporter<Metric>, IPullMetricExporter {
        public override ExportResult Export(in Batch<Metric> batch) {
            Batch = batch;
            return ExportResult.Success;
        }

        public Batch<Metric>   Batch   { get; set; }
        public Func<int, bool> Collect { get; set; }
    }
}