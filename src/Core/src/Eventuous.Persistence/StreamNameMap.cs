// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class StreamNameMap {
    readonly TypeMap<Func<AggregateId, StreamName>> _typeMap = new();

    public void Register<TId>(Func<TId, StreamName> map) where TId : AggregateId
        => _typeMap.Add<TId>(id => map((TId)id));

    public StreamName GetStreamName<T, TId>(TId aggregateId) where TId : AggregateId where T : Aggregate
        => _typeMap.TryGetValue<TId>(out var map)
            ? map(aggregateId)
            : StreamNameFactory.For<T, TId>(aggregateId);
}
