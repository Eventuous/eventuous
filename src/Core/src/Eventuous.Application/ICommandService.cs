// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public interface ICommandService {
    Task<Result> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<T> : ICommandService where T : Aggregate { }

public interface ICommandService<T, TState, TId>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : AggregateId {
    Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}