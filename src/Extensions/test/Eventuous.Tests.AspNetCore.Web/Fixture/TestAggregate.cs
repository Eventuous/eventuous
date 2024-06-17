namespace Eventuous.Tests.AspNetCore.Web.Fixture;

class Brooking : Aggregate<BrookingState>;

record BrookingState : State<BrookingState>;