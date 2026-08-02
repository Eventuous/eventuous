// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedTypeParameter

namespace Eventuous;

public interface ICommandService<TState> where TState : State<TState>, new() {
    Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;
}

public interface ICommandService<T, TState, TId> : ICommandService<TState>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id;