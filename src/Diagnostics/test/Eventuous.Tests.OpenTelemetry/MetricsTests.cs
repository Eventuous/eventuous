using DotNet.Testcontainers.Containers;
using Eventuous.Sut.Subs;
using Eventuous.Tests.OpenTelemetry.Fakes;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace Eventuous.Tests.OpenTelemetry;

public abstract class MetricsTestsBase<T, TContainer, TProducer, TSubscription, TSubscriptionOptions>(T fixture, ITestOutputHelper outputHelper)
    : IAsyncLifetime, IClassFixture<T> where T : MetricsSubscriptionFixtureBase<TContainer, TProducer, TSubscription, TSubscriptionOptions>
    where TContainer : DockerContainer
    where TProducer : class, IEventProducer
    where TSubscription : EventSubscriptionWithCheckpoint<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionWithCheckpointOptions {

    [Fact]
    public void ShouldMeasureSubscriptionGapCount() {
        Assert.NotNull(_values);
        var gapCount = GetValue(_values, SubscriptionMetrics.GapCountMetricName)!;
        var duration = GetValue(_values, SubscriptionMetrics.ProcessingRateName)!;

        var expectedGap = fixture.Count - fixture.Counter.Count;

        gapCount.Should().NotBeNull();
        gapCount.Value.Should().BeInRange(expectedGap - 20, expectedGap + 20);
        GetTag(gapCount, SubscriptionMetrics.SubscriptionIdTag).Should().Be(fixture.SubscriptionId);
        GetTag(gapCount, "test").Should().Be("foo");

        duration.Should().NotBeNull();
        GetTag(duration, SubscriptionMetrics.SubscriptionIdTag).Should().Be(fixture.SubscriptionId);
        GetTag(duration, SubscriptionMetrics.MessageTypeTag).Should().Be(TestEvent.TypeName);
        GetTag(duration, "test").Should().Be("foo");
    }

    static MetricValue? GetValue(MetricValue[] values, string metric)
        => values.FirstOrDefault(x => x.Name == metric);

    static object GetTag(MetricValue metric, string key) {
        var index = metric.Keys.Select((x, i) => (x, i)).First(x => x.x == key).i;

        return metric.Values[index];
    }

    public async Task InitializeAsync() {
        var testEvents = fixture.Auto.CreateMany<TestEvent>(fixture.Count).ToList();
        await fixture.Producer.Produce(fixture.Stream, testEvents, new Metadata());

        while (fixture.Counter.Count < fixture.Count / 2) {
            await Task.Delay(100);
        }

        fixture.Exporter.Collect(Timeout.Infinite);
        _values = fixture.Exporter.CollectValues();

        foreach (var value in _values) {
            outputHelper.WriteLine(value.ToString());
        }
    }

    public Task DisposeAsync() {
        _es.Dispose();

        return Task.CompletedTask;
    }

    readonly TestEventListener _es = new(outputHelper);

    MetricValue[]? _values;
}
