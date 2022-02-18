using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.OpenTelemetry;

public sealed class MetricsTests : IAsyncLifetime, IDisposable {
    static MetricsTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    const string SubscriptionId = "test-sub";

    readonly StreamName _stream;

    public MetricsTests(ITestOutputHelper outputHelper) {
        _exporter = new TestExporter();
        _stream   = new StreamName($"test-{Guid.NewGuid():N}");
        _output   = outputHelper;

        _es = new TestEventListener(outputHelper);

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
    public void CollectorShouldNotFail() {
        _exporter.Collect(Timeout.Infinite).Should().BeTrue();
    }

    [Fact]
    public void ShouldMeasureSubscriptionGapCount() {
        _exporter.Collect(Timeout.Infinite);
        var values = _exporter.CollectValues();
        var gapCount = GetValue(values, SubscriptionMetrics.GapCountMetricName)!;
        gapCount.Should().NotBeNull();
        gapCount.LongValue.Should().BeInRange(Count / 2, Count);
        GetTag(gapCount, SubscriptionMetrics.SubscriptionIdTag).Should().Be(SubscriptionId);
    }

    [Fact]
    public void ShouldMeasureConsumeDuration() {
        _exporter.Collect(Timeout.Infinite);
        var values = _exporter.CollectValues();
        var duration = GetValue(values, SubscriptionMetrics.ProcessingRateName)!;
        duration.Should().NotBeNull();
        GetTag(duration, SubscriptionMetrics.SubscriptionIdTag).Should().Be(SubscriptionId);
        GetTag(duration, SubscriptionMetrics.MessageTypeTag).Should().Be(TestEvent.TypeName);
    }

    static MetricValue? GetValue(MetricValue[] values, string metric)
        => values.FirstOrDefault(x => x.Name == metric);

    static object GetTag(MetricValue metric, string key) {
        var index = metric.Keys.Select((x, i) => (x, i)).First(x => x.x == key).i;
        return metric.Values[index];
    }

    const int Count = 1000;

    public async Task InitializeAsync() {
        var testEvents = IntegrationFixture.Instance.Auto.CreateMany<TestEvent>(Count).ToList();
        var producer   = _host.Services.GetRequiredService<IEventProducer>();
        await producer.Produce(_stream, testEvents);
        await Task.Delay(1000);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    readonly TestServer        _host;
    readonly TestExporter      _exporter;
    readonly TestEventListener _es;
    readonly ITestOutputHelper _output;

    class TestHandler : BaseEventHandler {
        public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            await Task.Delay(10, context.CancellationToken);
            return EventHandlingStatus.Success;
        }
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
                        new MetricValue(metric.Name, metricState.Item1, metricState.Item2, metricState.Item3)
                    );
                }
            }

            return values.ToArray();

            static bool TryGetMetric(
                ref MetricPointsAccessor.Enumerator enumerator,
                out (string[], object[], long)  state
            ) {
                if (!enumerator.MoveNext()) {
                    state = default;
                    return false;
                }

                var metricPoint = enumerator.Current;
                state = (metricPoint., metricPoint.Values, metricPoint.LongValue);
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