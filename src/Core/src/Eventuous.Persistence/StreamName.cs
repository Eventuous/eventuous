// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class StreamNameFactory {
    public static StreamName For<T, TId>(TId id) where T : Aggregate where TId : Id
        => new($"{typeof(T).Name}-{Ensure.NotEmptyString(id.ToString())}");

    public static StreamName For<T, TState, TId>(TId id) where T : Aggregate<TState> where TState : State<TState>, new() where TId : Id
        => new($"{typeof(T).Name}-{Ensure.NotEmptyString(id.ToString())}");
}
