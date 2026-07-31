// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using JetBrains.Annotations;

namespace Eventuous.Tests.Shared.Analyzers;

file record TestState : State<TestState> {
    public TestState() {
        On<Events.RoomBooked>((state, _) => state);
    }
}

[UsedImplicitly]
file class TestAggregate : Aggregate<TestState> {
    [UsedImplicitly]
    public void Process() => Apply(new Events.RoomBooked("1", DateTime.Now, DateTime.Now.AddDays(1), 100));
}

file static class Events {
    [PublicAPI]
    public record RoomBooked(string RoomId, DateTime CheckIn, DateTime CheckOut, decimal Price);
}
