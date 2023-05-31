// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class StreamNameMap {
    readonly TypeMap<Func<Id, StreamName>> _typeMap = new();

    public void Register<TId>(Func<TId, StreamName> map) where TId : Id
        => _typeMap.Add<TId>(id => map((TId)id));

    public StreamName GetStreamName<T, TId>(TId aggregateId) where TId : Id where T : Aggregate
        => _typeMap.TryGetValue<TId>(out var map)
            ? map(aggregateId)
            : StreamNameFactory.For<T, TId>(aggregateId);

    public StreamName GetStreamName<TId>(TId id) where TId : Id
        => _typeMap.TryGetValue<TId>(out var map)
            ? map(id)
            : throw new StreamNameMapNotFound<TId>(id);
}

public class StreamNameMapNotFound<TId> : Exception where TId : Id {
    public StreamNameMapNotFound(TId id) : base($"No stream name map found for {typeof(TId).Name} with value {id}") { }
}
