
namespace Eventuous.Tests.Aggregates;


using Testing;

public class StandardBehaviorSpec : AggregateSpec<BehaviorAggregate> {
   
    protected override object[] GivenEvents() => [
      
    ];

    protected override void When(BehaviorAggregate aggregate) => aggregate.DoStuff(new TestCommand());

    [Fact]
    public void should_be_exceptional() {
        Then().Changes.Should().Contain(new TestEvents.BecameExceptional());
    }

}

public class ExceptionalBehaviorSpec : AggregateSpec<BehaviorAggregate> {
   
    protected override object[] GivenEvents() => [
      new TestEvents.BecameExceptional()
    ];

    protected override void When(BehaviorAggregate aggregate) => aggregate.DoStuff(new TestCommand());

    [Fact]
    public void should_be_exceptional() {
        Then().Changes.Should().Contain(new TestEvents.BecameStandard());
    }

}

public class BehaviorAggregate
    : Aggregate<TestState>{


    private Action<TestCommand> _behavior;

    public BehaviorAggregate() {
        Become(Standard);
    }

    public void Become(Action<TestCommand> behavior) {
        _behavior = behavior;
    }

    public void DoStuff(TestCommand command) {
        _behavior(command);
    }
    
    void Standard(TestCommand obj) {
        Apply(new TestEvents.BecameExceptional());
    }

    void Exceptional(TestCommand obj) {
        Apply(new TestEvents.BecameStandard());
    }

    protected override void When(object evt) {

        switch (evt) {
           case TestEvents.BecameExceptional: 
            Become(Exceptional);
                break; 
           case TestEvents.BecameStandard: 
            Become(Standard);
                break;
        }
        
    }
}

public record TestCommand;

public record TestState : State<TestState,TestId>;

public record TestId(string Value) : Id(Value);

public  static class TestEvents {
    public record BecameExceptional;
    public record BecameStandard;
    
}