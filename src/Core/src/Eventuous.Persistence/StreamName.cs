// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class StreamNameFactory {
    public static StreamName For<T, TId>(TId entityId) where T : Aggregate where TId : AggregateId
        => new($"{typeof(T).Name}-{Ensure.NotEmptyString(entityId.ToString())}");

    public static StreamName For<T, TState, TId>(TId entityId) where T : Aggregate<TState> where TState : State<TState>, new() where TId : AggregateId
        => new($"{typeof(T).Name}-{Ensure.NotEmptyString(entityId.ToString())}");
}
