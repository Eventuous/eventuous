namespace Eventuous.AspNetCore.Tests.Sut;

public class TestAggregate : Aggregate<TestState, TestId> {
    public TestDependency Dependency { get; }

    public TestAggregate(TestDependency dependency) => Dependency = dependency;
}

public record TestState : AggregateState<TestState, TestId> {
}

public record TestId(string Value) : AggregateId(Value);
