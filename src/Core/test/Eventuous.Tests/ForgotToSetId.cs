using Eventuous.Tests.Fixtures;

namespace Eventuous.Tests;

public class ForgotToSetId : NaiveFixture {
    public ForgotToSetId() => Service = new TestService(this.AggregateStore);

    [Fact]
    public async Task ShouldFailWithNoId() {
        var cmd     = new DoIt(Auto.Create<string>());
        var result  = await Service.Handle(cmd, default);
        result.Success.Should().BeFalse();
        (result as ErrorResult<TestState, TestId>)!.Exception.Should().BeOfType<Exceptions.InvalidIdException>();
    }

    TestService Service { get; }

    class TestService : ApplicationService<TestAggregate, TestState, TestId> {
        public TestService(IAggregateStore store) : base(store)
            => OnNew<DoIt>((test, cmd) => test.DoIt(new TestId(cmd.Id)));
    }

    record DoIt(string Id);

    class TestAggregate : Aggregate<TestState, TestId> {
        public void DoIt(TestId id) => Apply(new TestEvent(id));
    }

    record TestState : AggregateState<TestState, TestId>;

    record TestId : AggregateId {
        public TestId(string value) : base(value) { }
    }

    record TestEvent(string Id);
}
