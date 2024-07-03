namespace Eventuous.Tests.Extensions.AspNetCore.Fixture;

class Brooking : Aggregate<BrookingState>;

record BrookingState : State<BrookingState>;