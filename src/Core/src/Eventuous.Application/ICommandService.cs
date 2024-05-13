// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedTypeParameter

namespace Eventuous;

public interface ICommandService {
    Task<Result> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<TAggregate> : ICommandService where TAggregate : Aggregate;

public interface IFuncCommandService<TState> : ICommandService where TState : State<TState>;

public interface IStateCommandService<TState> where TState : State<TState>, new() {
    new Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<TAggregate, TState> : IStateCommandService<TState>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new();

public interface ICommandService<TAggregate, TState, TId> : ICommandService<TAggregate, TState>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id;