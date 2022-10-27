namespace Eventuous.Tests.AspNetCore.Sut;

public class TestAggregate : Aggregate<TestState> {
    public TestDependency Dependency { get; }

    public TestAggregate(TestDependency dependency) => Dependency = dependency;
}

public class AnotherTestAggregate : Aggregate<TestState> {
    public TestDependency Dependency { get; }

    public AnotherTestAggregate(TestDependency dependency) => Dependency = dependency;
}

public record TestState : State<TestState>;

public record TestId(string Value) : AggregateId(Value);
