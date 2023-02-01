// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous;

[Obsolete("Use CommandService instead"), PublicAPI]
public abstract class ApplicationService<TAggregate, TState, TId>
    : CommandService<TAggregate, TState, TId>
    where TAggregate : Aggregate<TState>, new()
    where TState : State<TState>, new()
    where TId : AggregateId {
    protected ApplicationService(
        IAggregateStore           store,
        AggregateFactoryRegistry? factoryRegistry = null,
        StreamNameMap?            streamNameMap   = null,
        TypeMapper?               typeMap         = null
    ) : base(store, factoryRegistry, streamNameMap, typeMap) { }
}