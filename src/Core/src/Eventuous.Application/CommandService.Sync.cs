// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public abstract partial class CommandService<TAggregate, TState, TId> {
    /// <summary>
    /// Register a handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNew<TCommand>(
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>?              resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.New, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register a handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnExisting<TCommand>(
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>?              resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.Existing, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnAny<TCommand>(
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>?              resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.Any, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAny<TCommand>(
        GetIdFromCommandAsync<TId, TCommand> getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>?         resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.Any, getId, action, resolveStore ?? DefaultResolve<TCommand>());
}
