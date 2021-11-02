using System.Diagnostics;
using Eventuous.Producers;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeOneTests : SubscriptionFixture {
    readonly ActivityListener _listener;

    public PublishAndSubscribeOneTests(ITestOutputHelper outputHelper) : base(outputHelper) {
        _listener = new ActivityListener {
            ShouldListenTo = _ => true, //_.Name == Instrumentation.Name,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => Log.LogInformation(
                "Started {Activity} with {Id}, parent {ParentId}",
                activity.DisplayName,
                activity.Id,
                activity.ParentId
            ),
            ActivityStopped = activity => Log.LogInformation("Stopped {Activity}", activity.DisplayName)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();
        Handler.AssertThat().Any(x => x as TestEvent == testEvent);

        await Producer.Produce(Stream, testEvent);

        await Handler.Validate(10.Seconds());

        CheckpointStore.Last.Position.Should().Be(0);
    }
}