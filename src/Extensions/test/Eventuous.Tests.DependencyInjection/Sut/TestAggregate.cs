namespace Eventuous.Tests.AspNetCore.Sut;

public class TestAggregate(TestDependency dependency) : Aggregate<TestState> {
    public TestDependency Dependency { get; } = dependency;
}

public class AnotherTestAggregate(TestDependency dependency) : Aggregate<TestState> {
    public TestDependency Dependency { get; } = dependency;
}

public record TestState : State<TestState>;

public record TestId(string Value) : Id(Value);
