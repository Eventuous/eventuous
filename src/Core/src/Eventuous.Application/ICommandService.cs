// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedTypeParameter
namespace Eventuous;

public interface ICommandService {
    Task<Result> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<T> : ICommandService where T : Aggregate { }

public interface IFuncCommandService<T> : ICommandService where T : State<T> { }

public interface IStateCommandService<TState>
    where TState : State<TState>, new() {
    Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<T, TState, TId> : IStateCommandService<TState>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : AggregateId {
}
