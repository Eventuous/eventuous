using DotNet.Testcontainers.Containers;
using Eventuous.Sut.Subs;
using Eventuous.Tests.OpenTelemetry.Fakes;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace Eventuous.Tests.OpenTelemetry;

public abstract class MetricsTestsBase<T, TContainer, TProducer, TSubscription, TSubscriptionOptions>(ITestOutputHelper outputHelper)
    : IAsyncLifetime
    where T : MetricsSubscriptionFixtureBase<TContainer, TProducer, TSubscription, TSubscriptionOptions>, new()
    where TContainer : DockerContainer
    where TProducer : class, IEventProducer
    where TSubscription : EventSubscriptionWithCheckpoint<TSubscriptionOptions>, IMeasuredSubscription
    where TSubscriptionOptions : SubscriptionWithCheckpointOptions {
    T Fixture { get; } = new T();

    [Fact]
    public void ShouldMeasureSubscriptionGapCount() {
        outputHelper.WriteLine($"Stream {Fixture.Stream}");
        Assert.NotNull(_values);
        var gapCount    = GetValue(_values, SubscriptionMetrics.GapCountMetricName)!;
        var expectedGap = Fixture.Count - Fixture.Counter.Count;

        gapCount.Should().NotBeNull();
        gapCount.Value.Should().BeInRange(expectedGap - 20, expectedGap + 20);
        gapCount.CheckTag(SubscriptionMetrics.SubscriptionIdTag, Fixture.SubscriptionId);
        gapCount.CheckTag(Fixture.DefaultTagKey, Fixture.DefaultTagValue);
    }

    [Fact]
    public void ShouldMeasureSubscriptionDuration() {
        outputHelper.WriteLine($"Stream {Fixture.Stream}");
        Assert.NotNull(_values);
        var duration = GetValue(_values, SubscriptionMetrics.ProcessingRateName)!;

        duration.Should().NotBeNull();
        duration.CheckTag(SubscriptionMetrics.SubscriptionIdTag, Fixture.SubscriptionId);
        duration.CheckTag(Fixture.DefaultTagKey, Fixture.DefaultTagValue);
        duration.CheckTag(SubscriptionMetrics.MessageTypeTag, TestEvent.TypeName);
    }

    static MetricValue? GetValue(MetricValue[] values, string metric)
        => values.FirstOrDefault(x => x.Name == metric);

    public async Task InitializeAsync() {
        await Fixture.InitializeAsync();
        var testEvents = Fixture.Auto.CreateMany<TestEvent>(Fixture.Count).ToList();
        await Fixture.Producer.Produce(Fixture.Stream, testEvents, new Metadata());

        while (Fixture.Counter.Count < Fixture.Count / 2) {
            await Task.Delay(100);
        }

        Fixture.Exporter.Collect(Timeout.Infinite);
        _values = Fixture.Exporter.CollectValues();

        foreach (var value in _values) {
            outputHelper.WriteLine(value.ToString());
        }
    }

    public async Task DisposeAsync() {
        await Fixture.DisposeAsync();
        _es.Dispose();
    }

    readonly TestEventListener _es = new(outputHelper);

    MetricValue[]? _values;
}

static class TagExtensions {
    public static void CheckTag(this MetricValue metric, string tag, string expectedValue) {
        metric.GetTag(tag).Should().Be(expectedValue);
    }

    static object GetTag(this MetricValue metric, string key) {
        var index = metric.Keys.Select((x, i) => (x, i)).First(x => x.x == key).i;

        return metric.Values[index];
    }
}
