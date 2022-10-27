using Eventuous.Tests.Fixtures;

namespace Eventuous.Tests;

public class ForgotToSetId : NaiveFixture {
    public ForgotToSetId() => Service = new TestService(this.AggregateStore);

    [Fact]
    public async Task ShouldFailWithNoId() {
        var cmd    = new DoIt(Auto.Create<string>());
        var result = await Service.Handle(cmd, default);
        result.Success.Should().BeTrue();
    }

    TestService Service { get; }

    class TestService : ApplicationService<TestAggregate, TestState, TestId> {
        public TestService(IAggregateStore store) : base(store)
            => OnNew<DoIt>(
                cmd => new TestId(cmd.Id),
                (test, cmd) => test.Process()
            );
    }

    record DoIt(string Id);

    class TestAggregate : Aggregate<TestState> {
        public void Process() => Apply(new TestEvent());
    }

    record TestState : State<TestState>;

    record TestId : AggregateId {
        public TestId(string value) : base(value) { }
    }

    record TestEvent;
}
