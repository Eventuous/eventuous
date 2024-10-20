using Eventuous.Testing;

namespace Eventuous.Tests.AggregateWithId;

public class OperateOnAggregateWithId : AggregateWithIdSpec<TestAggregate, TestState, TestId> {
    protected override void When(TestAggregate aggregate) => aggregate.Process();

    const string IdValue = "test";

    protected override TestId? Id { get; } = new(IdValue);

    [Test]
    public void should_emit_event() => Emitted(new TestEvent());

    [Test]
    public void should_set_id() => Then().State.Id.Value.Should().Be(IdValue);
}

public class TestAggregate : Aggregate<TestState> {
    public void Process() => Apply(new TestEvent());
}

public record TestState : State<TestState, TestId>;

public record TestId(string Value) : Id(Value);

record TestEvent;
