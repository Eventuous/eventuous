using Eventuous.Tests.Fixtures;

namespace Eventuous.Tests;

public class ForgotToSetId : NaiveFixture {
    public ForgotToSetId() => Service = new(this.EventStore);

    [Test]
    public async Task ShouldFailWithNoId(CancellationToken cancellationToken) {
        var cmd    = new DoIt(Guid.NewGuid().ToString());
        var result = await Service.Handle(cmd, cancellationToken);
        await Assert.That(result.Success).IsTrue();
    }

    TestService Service { get; }

    public class TestService : CommandService<TestAggregate, TestState, TestId> {
        public TestService(IEventStore store) : base(store)
            => On<DoIt>().InState(ExpectedState.New).GetId(cmd => new(cmd.Id)).Act((test, _) => test.Process());
    }

    record DoIt(string Id);

    public class TestAggregate : Aggregate<TestState> {
#pragma warning disable EVTC001
        public void Process() => Apply(new TestEvent());
#pragma warning restore EVTC001
    }

    public record TestState : State<TestState>;

    public record TestId(string Value) : Id(Value);

    record TestEvent;
}
