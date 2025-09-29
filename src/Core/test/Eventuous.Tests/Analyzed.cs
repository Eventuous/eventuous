// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Tests;

public class Analyzed {

}

file record TestState : State<TestState> { }

file class TestAggregate : Aggregate<TestState> {
    public void Process() => Apply(new Events.BookRoom("1", DateTime.Now, DateTime.Now.AddDays(1), 100));
}

file static class Events {
    public record BookRoom(string RoomId, DateTime CheckIn, DateTime CheckOut, decimal Price);
}
