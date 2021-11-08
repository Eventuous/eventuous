using Eventuous.Diagnostics.OpenTelemetry.Subscriptions;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Sut.Subs;
using Eventuous.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Eventuous.Tests.EventStore;

public class MetricsTests : IDisposable, IAsyncLifetime {
    static MetricsTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    const string SubscriptionId = "test-sub";

    readonly StreamName _stream;

    public MetricsTests(ITestOutputHelper outputHelper) {
        _exporter = new TestExporter();
        _stream   = new StreamName($"test-{Guid.NewGuid():N}");
        _output   = outputHelper;

        _es = new TestEventListener(outputHelper, "eventuous", "OpenTelemetry");

        var builder = new WebHostBuilder()
            .Configure(_ => { })
            .ConfigureServices(
                services => {
                    services.AddSingleton(IntegrationFixture.Instance.Client);
                    services.AddEventProducer<EventStoreProducer>();

                    services.AddCheckpointStore<NoOpCheckpointStore>();

                    services.AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
                        SubscriptionId,
                        builder => builder
                            .Configure(options => options.StreamName = _stream)
                            .AddEventHandler<TestHandler>()
                    );

                    services.AddOpenTelemetryMetrics(
                        builder => builder
                            .AddEventuousSubscriptions()
                            .AddReader(new BaseExportingMetricReader(_exporter))
                    );
                }
            )
            .ConfigureLogging(cfg => cfg.AddXunit(outputHelper));

        _host = new TestServer(builder);
    }

    [Fact]
    public async Task ShouldMeasureSubscription() {
        const int count = 1000;

        var testEvents = IntegrationFixture.Instance.Auto.CreateMany<TestEvent>(count).ToList();
        var producer   = _host.Services.GetRequiredService<IEventProducer>();
        await producer.Produce(_stream, testEvents);

        await Task.Delay(1000);

        _exporter.Collect(Timeout.Infinite).Should().BeTrue();
        var values = _exporter.CollectValues();

        _output.WriteLine($"Gap: {values[0].LongValue}");

        var gapCount = values.First(x => x.Name == SubscriptionGapMetric.MetricName);
        gapCount.LongValue.Should().BeInRange(count / 2, count);
        gapCount.Keys[0].Should().Be("subscription-id");
        gapCount.Values[0].Should().Be(SubscriptionId);
    }

    readonly TestServer        _host;
    readonly TestExporter      _exporter;
    readonly TestEventListener _es;
    readonly ITestOutputHelper _output;

    public Task InitializeAsync() => _host.Host.StartAsync();

    public Task DisposeAsync() => _host.Host.StopAsync();

    class TestHandler : IEventHandler {
        public async ValueTask HandleEvent(IMessageConsumeContext context)
            => await Task.Delay(10, context.CancellationToken);
    }

    [ExportModes(ExportModes.Pull)]
    class TestExporter : BaseExporter<Metric>, IPullMetricExporter {
        public override ExportResult Export(in Batch<Metric> batch) {
            Batch = batch;
            return ExportResult.Success;
        }

        Batch<Metric> Batch { get; set; }

        public Func<int, bool> Collect { get; set; } = null!;

        public MetricValue[] CollectValues() {
            var values = new List<MetricValue>();

            foreach (var metric in Batch) {
                if (metric == null) continue;

                var enumerator = metric.GetMetricPoints().GetEnumerator();

                if (TryGetMetric(ref enumerator, out var metricState)) {
                    values.Add(
                        new MetricValue(
                            metric.Name,
                            metricState.Item1,
                            metricState.Item2,
                            metricState.Item3
                        )
                    );
                }
            }

            return values.ToArray();

            static bool TryGetMetric(
                ref BatchMetricPoint.Enumerator enumerator,
                out (string[], object[], long)  state
            ) {
                if (!enumerator.MoveNext()) {
                    state = default;
                    return false;
                }

                ref var metricPoint = ref enumerator.Current;
                state = (metricPoint.Keys, metricPoint.Values, metricPoint.LongValue);
                return true;
            }
        }
    }

    public void Dispose() {
        _host.Dispose();
        _exporter.Dispose();
        _es.Dispose();
    }
}

record MetricValue(string Name, string[] Keys, object[] Values, long LongValue);