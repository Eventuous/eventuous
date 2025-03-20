using Eventuous.TestHelpers.TUnit;
using Eventuous.Tests.OpenTelemetry.Fakes;
using Eventuous.Tests.Subscriptions.Base;

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace Eventuous.Tests.OpenTelemetry;

public abstract class MetricsTestsBase(IMetricsSubscriptionFixtureBase fixture) {
    protected async Task ShouldMeasureSubscriptionGapCountBase() {
        TestContext.Current?.OutputWriter.WriteLine($"Stream {fixture.Stream}");
        await Assert.That(_values).IsNotNull();
        var gapCount    = GetValue(_values!, SubscriptionMetrics.GapCountMetricName)!;
        var expectedGap = fixture.Count - fixture.Counter.Count;

        await Assert.That(gapCount).IsNotNull();
        // await Assert.That(gapCount.Value).IsBetween(expectedGap - 20, expectedGap + 20);
        await gapCount.CheckTag(SubscriptionMetrics.SubscriptionIdTag, fixture.SubscriptionId);
        await gapCount.CheckTag(fixture.DefaultTagKey, fixture.DefaultTagValue);
    }

    // [Fact]
    // [Trait("Category", "Diagnostics")]
    // public void ShouldMeasureSubscriptionDuration() {
    //     Fixture.Output?.WriteLine($"Stream {Fixture.Stream}");
    //     Assert.NotNull(_values);
    //     var duration = GetValue(_values, SubscriptionMetrics.ProcessingRateName)!;
    //
    //     duration.Should().NotBeNull();
    //     duration.CheckTag(SubscriptionMetrics.SubscriptionIdTag, Fixture.SubscriptionId);
    //     duration.CheckTag(Fixture.DefaultTagKey, Fixture.DefaultTagValue);
    //     duration.CheckTag(SubscriptionMetrics.MessageTypeTag, TestEvent.TypeName);
    // }

    static MetricValue? GetValue(MetricValue[] values, string metric) => values.FirstOrDefault(x => x.Name == metric);

    [Before(Test)]
    public async Task InitializeAsync() {
        var testEvents = TestEvent.CreateMany(fixture.Count);
        await fixture.Producer.Produce(fixture.Stream, testEvents, new());

        while (fixture.Counter.Count < fixture.Count / 2) {
            await Task.Delay(100);
        }

        fixture.Exporter.Collect(Timeout.Infinite);
        _values = fixture.Exporter.CollectValues();

        foreach (var value in _values) {
            TestContext.Current?.OutputWriter.WriteLine(value.ToString());
        }
    }

    [After(Test)]
    public void Teardown() {
        _es.Dispose();
    }

    readonly TestEventListener _es = new(null, "OpenTelemetry");

    MetricValue[]? _values;
}

static class TagExtensions {
    public static async Task CheckTag(this MetricValue metric, string tag, string expectedValue) {
        await Assert.That(metric.GetTag(tag)).IsEqualTo(expectedValue);
    }

    static object GetTag(this MetricValue metric, string key) {
        var index = metric.Keys.Select((x, i) => (x, i)).First(x => x.x == key).i;

        return metric.Values[index];
    }
}
