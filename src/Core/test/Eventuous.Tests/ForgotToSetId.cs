using Eventuous.Tests.Fixtures;

namespace Eventuous.Tests;

public class ForgotToSetId : NaiveFixture {
    public ForgotToSetId() => Service = new(this.EventStore);

    [Fact]
    public async Task ShouldFailWithNoId() {
        var cmd    = new DoIt(Auto.Create<string>());
        var result = await Service.Handle(cmd, default);
        result.Success.Should().BeTrue();
    }

    TestService Service { get; }

    class TestService : CommandService<TestAggregate, TestState, TestId> {
        public TestService(IEventStore store) : base(store)
            => On<DoIt>().InState(ExpectedState.New).GetId(cmd => new(cmd.Id)).Act((test, _) => test.Process());
    }

    record DoIt(string Id);

    class TestAggregate : Aggregate<TestState> {
        public void Process() => Apply(new TestEvent());
    }

    record TestState : State<TestState>;

    record TestId(string Value) : Id(Value);

    record TestEvent;
}
