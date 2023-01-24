// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Process;

public abstract class ProcessManager<T, TState, TId> : ApplicationService<T, TState, TId>
    where T : Process<TState> where TState : ProcessState<TState>, new() where TId : ProcessId {
    protected ProcessManager(
        IAggregateStore           store,
        AggregateFactoryRegistry? factoryRegistry = null,
        StreamNameMap?            streamNameMap   = null,
        TypeMapper?               typeMap         = null
    ) : base(store, factoryRegistry, streamNameMap, typeMap) { }
}