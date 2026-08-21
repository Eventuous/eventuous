// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous.Tests.Shared.Analyzers;

file record TestState : State<TestState> {
    public TestState() {
        On<Events.RoomBooked>((state, _) => state);
        On<Events.RoomRegistered>((state, _) => state);
    }
}

[UsedImplicitly]
file class TestEventHandler : Eventuous.Subscriptions.EventHandler {
    public TestEventHandler() {
        On<Events.RoomBooked>(_ => new());
    }
}

[UsedImplicitly]
file class TestAggregate : Aggregate<TestState> {
    [UsedImplicitly]
    public void Process() => Apply(new Events.RoomBooked("1", DateTime.Now, DateTime.Now.AddDays(1), 100));
}

[UsedImplicitly]
file class TestFunctionalService : CommandService<TestState> {
    public TestFunctionalService(IEventStore store) : base(store) {
        On<CancelBooking>()
            .InState(ExpectedState.Existing)
            .GetStream(cmd => new StreamName($"Booking-{cmd.BookingId}"))
            .ActAsync((state, events, cmd, ct) => Task.FromResult<IEnumerable<object>>(new object[] { new Events.BookingCancelled(cmd.BookingId) }));
    }
}

file record CancelBooking(string BookingId);

file static class TypeRegistration {
    // Events.RoomRegistered has no [EventType] but is registered explicitly, so it must not be flagged
    [UsedImplicitly]
    public static void Register() => TypeMap.Instance.AddType<Events.RoomRegistered>("V1.RoomRegistered");
}

[UsedImplicitly]
file class StateReplay {
    // Event replay pattern from issue #535: payloads are deserialized by the type map,
    // so they are statically typed as object at the call site and must not trigger EVTC001
    [UsedImplicitly]
    public TestState Replay(object?[] payloads) {
        var state = new TestState();

        foreach (var payload in payloads) {
            if (payload != null) state = state.When(payload);
        }

        return state;
    }
}

file static class Events {
    [PublicAPI]
    public record RoomBooked(string RoomId, DateTime CheckIn, DateTime CheckOut, decimal Price);

    [PublicAPI]
    public record BookingCancelled(string BookingId);

    [PublicAPI]
    public record RoomRegistered(string RoomId);
}
