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
        // var expectedGap = fixture.Count - fixture.Counter.Count;

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

/// <summary>
/// Observes <c>eventuous.subscription.gap.count</c> through the real pipeline — the meter registered by
/// <c>AddEventuousSubscriptions</c>, the measure resolved from DI, the checkpoint-commit listener and the gap
/// arithmetic in <see cref="SubscriptionMetrics"/> — rather than calling the subscription's measure directly.
/// Reproduces GitHub #586 at the metric level: a longer, unrelated stream must not inflate the gap reported for a
/// caught-up stream subscription.
/// </summary>
public abstract class SubscriptionGapMetricsTestsBase(IMetricsSubscriptionFixtureBase fixture) {
    protected async Task ShouldReportZeroGapWhenCaughtUp(CancellationToken cancellationToken) {
        var other = new StreamName($"other-{Guid.NewGuid():N}");
        await fixture.Producer.Produce(other, TestEvent.CreateMany(fixture.Count * 2), new(), cancellationToken: cancellationToken);
        await fixture.Producer.Produce(fixture.Stream, TestEvent.CreateMany(fixture.Count), new(), cancellationToken: cancellationToken);

        await WaitUntil(() => fixture.Counter.Count >= fixture.Count, TimeSpan.FromSeconds(30), cancellationToken);
        await Assert.That(fixture.Counter.Count).IsEqualTo(fixture.Count);

        // The checkpoint behind the gap commits on a batch or a delay, so the gauge converges on zero rather than
        // reading it the moment the last event is handled.
        var gap = await WaitForGapCount(0, TimeSpan.FromSeconds(15), cancellationToken);

        await Assert.That(gap).IsNotNull();
        await Assert.That(gap!.Value).IsEqualTo(0d);
        await gap.CheckTag(SubscriptionMetrics.SubscriptionIdTag, fixture.SubscriptionId);
        await gap.CheckTag(fixture.DefaultTagKey, fixture.DefaultTagValue);
    }

    /// <summary>
    /// Polls the exporter until the gap gauge reads <paramref name="expected"/> or the timeout elapses, returning the
    /// last observation either way so a failure shows the value that was actually reported.
    /// </summary>
    async Task<MetricValue?> WaitForGapCount(double expected, TimeSpan timeout, CancellationToken cancellationToken) {
        MetricValue? gap      = null;
        var          deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            fixture.Exporter.Collect(Timeout.Infinite);
            gap = fixture.Exporter.CollectValues().FirstOrDefault(x => x.Name == SubscriptionMetrics.GapCountMetricName);
            TestContext.Current?.OutputWriter.WriteLine($"Gap: {gap?.Value.ToString() ?? "not reported"}");

            if (gap?.Value == expected) break;

            await Task.Delay(200, cancellationToken);
        }

        return gap;
    }

    static async Task WaitUntil(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken) {
        var deadline = DateTime.UtcNow + timeout;

        while (!condition() && DateTime.UtcNow < deadline) {
            await Task.Delay(100, cancellationToken);
        }
    }

    [After(Test)]
    public void Teardown() => _es.Dispose();

    readonly TestEventListener _es = new(null, "OpenTelemetry");
}

static class TagExtensions {
    extension(MetricValue metric) {
        public async Task CheckTag(string tag, string expectedValue) {
            await Assert.That(metric.GetTag(tag)).IsEqualTo(expectedValue);
        }

        object GetTag(string key) {
            var index = metric.Keys.Select((x, i) => (x, i)).First(x => x.x == key).i;

            return metric.Values[index];
        }
    }
}
