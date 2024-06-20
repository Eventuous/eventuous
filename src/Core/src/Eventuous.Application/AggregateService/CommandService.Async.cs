// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.CommandServiceDelegates;

namespace Eventuous;

public abstract partial class CommandService<TAggregate, TState, TId> {
    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.New).GetId(...).ActAsync(...).ResolveStore(...) instead")]
    protected void OnNewAsync<TCommand>(
            GetIdFromCommand<TId, TCommand>                   getId,
            ActOnAggregateAsync<TAggregate, TState, TCommand> action,
            ResolveStore<TCommand>?                           resolveStore = null
        ) where TCommand : class
        => On<TCommand>().InState(ExpectedState.New).GetId(getId).ActAsync(action).ResolveStore(resolveStore);

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Existing).GetId(...).ActAsync(...).ResolveStore(...) instead")]
    [PublicAPI]
    protected void OnExistingAsync<TCommand>(
            GetIdFromCommand<TId, TCommand>                   getId,
            ActOnAggregateAsync<TAggregate, TState, TCommand> action,
            ResolveStore<TCommand>?                           resolveStore = null
        ) where TCommand : class
        => On<TCommand>().InState(ExpectedState.Existing).GetId(getId).ActAsync(action).ResolveStore(resolveStore);

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Existing).GetIdAsync(...).ActAsync(...).ResolveStore(...) instead")]
    [PublicAPI]
    protected void OnExistingAsync<TCommand>(
            GetIdFromCommandAsync<TId, TCommand>              getId,
            ActOnAggregateAsync<TAggregate, TState, TCommand> action,
            ResolveStore<TCommand>?                           resolveStore = null
        ) where TCommand : class
    // => _handlers.AddHandler(ExpectedState.Existing, getId, action, resolveStore ?? DefaultResolve<TCommand>());
        => On<TCommand>().InState(ExpectedState.Existing).GetIdAsync(getId).ActAsync(action).ResolveStore(resolveStore);

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetId(...).ActAsync(...).ResolveStore(...) instead")]
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(
            GetIdFromCommand<TId, TCommand>                   getId,
            ActOnAggregateAsync<TAggregate, TState, TCommand> action,
            ResolveStore<TCommand>?                           resolveStore = null
        ) where TCommand : class
    // => _handlers.AddHandler(ExpectedState.Any, getId, action, resolveStore ?? DefaultResolve<TCommand>());
        => On<TCommand>().InState(ExpectedState.Any).GetId(getId).ActAsync(action).ResolveStore(resolveStore);

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetIdAsync(...).ActAsync(...).ResolveStore(...) instead")]
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(
            GetIdFromCommandAsync<TId, TCommand>              getId,
            ActOnAggregateAsync<TAggregate, TState, TCommand> action,
            ResolveStore<TCommand>?                           resolveStore = null
        ) where TCommand : class
    // => _handlers.AddHandler(ExpectedState.Any, getId, action, resolveStore ?? DefaultResolve<TCommand>());
        => On<TCommand>().InState(ExpectedState.Any).GetIdAsync(getId).ActAsync(action).ResolveStore(resolveStore);
}
