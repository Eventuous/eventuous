namespace Eventuous.Tests.AspNetCore.Web.Fixture;

class Brooking : Aggregate<BrookingState> {
    public override void Load(IEnumerable<object?> events) { }
}

record BrookingState : State<BrookingState>;