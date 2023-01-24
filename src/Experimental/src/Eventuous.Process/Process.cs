// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Process;

public abstract record ProcessId : AggregateId {
    protected ProcessId(string value) : base(value) { }
}

public abstract record ProcessState<T> : State<T> where T : ProcessState<T> { }

public abstract class Process<TState> : Aggregate<TState> where TState : ProcessState<TState>, new() { }