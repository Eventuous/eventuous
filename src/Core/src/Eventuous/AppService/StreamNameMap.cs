// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class StreamNameMap {
    readonly Dictionary<Type, Func<AggregateId, StreamName>> _map = new();

    public void Register<TId>(Func<TId, StreamName> map)
        where TId : AggregateId
        => _map.TryAdd(typeof(TId), id => map((TId)id));

    public StreamName GetStreamName<T, TId>(TId aggregateId)
        where TId : AggregateId where T : Aggregate {
        if (_map.TryGetValue(typeof(TId), out var map)) return map(aggregateId);

        _map[typeof(TId)] = id => StreamName.For<T, TId>((TId)id);

        return _map[typeof(TId)](aggregateId);
    }
}
