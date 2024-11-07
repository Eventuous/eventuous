// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class StreamNameFactory {
    public static StreamName For<TAggregate, TState, TId>(TId id) where TAggregate : Aggregate<TState> where TState : State<TState>, new() where TId : Id
        => new($"{typeof(TAggregate).Name}-{Ensure.NotEmptyString(id.Value)}");

    public static StreamName For<TId>(TId id) where TId : Id {
        var idTypeName = typeof(TId).Name;

        var idSpan = idTypeName.AsSpan();

        if (idSpan.EndsWith("Id")) {
            idSpan = idSpan[..^2];
        }

        return idSpan.Length > 0 ? new($"{idSpan}-{id.Value}") : new($"{idTypeName}-{id.Value}");
    }
}
