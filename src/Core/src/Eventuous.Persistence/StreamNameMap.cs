// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous;

public class StreamNameMap {
    readonly TypeMap<Func<Id, StreamName>> _typeMap = new();

    public void Register<TId>(Func<TId, StreamName> map) where TId : Id => _typeMap.Add<TId>(id => map((TId)id));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StreamName GetStreamName<T, TState, TId>(TId aggregateId) where TId : Id where T : Aggregate<TState> where TState : State<TState>, new()
        => _typeMap.TryGetValue<TId>(out var map) ? map(aggregateId) : StreamNameFactory.For<T, TState, TId>(aggregateId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StreamName GetStreamName<TId>(TId id) where TId : Id => _typeMap.TryGetValue<TId>(out var map) ? map(id) : StreamNameFactory.For(id);
}