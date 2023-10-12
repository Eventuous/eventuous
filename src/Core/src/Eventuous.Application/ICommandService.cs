// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedTypeParameter

namespace Eventuous;

public interface ICommandService {
    Task<Result> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<TAggregate> : ICommandService where TAggregate : Aggregate { }

public interface IFuncCommandService<TState> : ICommandService where TState : State<TState> { }

public interface IStateCommandService<TState>
    where TState : State<TState>, new() {
    Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<T, TState, TId> : IStateCommandService<TState>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id { }