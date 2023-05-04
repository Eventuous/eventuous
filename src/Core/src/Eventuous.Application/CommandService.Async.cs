// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public abstract partial class CommandService<TAggregate, TState, TId> {
    ResolveStore<TCommand> DefaultResolve<TCommand>() where TCommand : class {
        if (Store == null) {
            throw new ArgumentNullException(nameof(Store), "Store is not set");
        }

        return _ => Store;
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to create a new aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    protected void OnNewAsync<TCommand>(
        GetIdFromCommand<TId, TCommand>           getId,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        ResolveStore<TCommand>?                   resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.New, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnExistingAsync<TCommand>(
        GetIdFromCommand<TId, TCommand>           getId,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        ResolveStore<TCommand>?                   resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.Existing, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnExistingAsync<TCommand>(
        GetIdFromCommandAsync<TId, TCommand>      getId,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        ResolveStore<TCommand>?                   resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.Existing, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(
        GetIdFromCommand<TId, TCommand>           getId,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        ResolveStore<TCommand>?                   resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.Any, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(
        GetIdFromCommandAsync<TId, TCommand>      getId,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        ResolveStore<TCommand>?                   resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler(ExpectedState.Any, getId, action, resolveStore ?? DefaultResolve<TCommand>());

    /// <summary>
    /// Register an asynchronous handler for a command, which can figure out the aggregate instance by itself, and then return one.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Function, which returns some aggregate instance to store</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [PublicAPI]
    protected void OnAsync<TCommand>(
        GetIdFromCommand<TId, TCommand> getId,
        ArbitraryActAsync<TCommand>     action,
        ResolveStore<TCommand>?         resolveStore = null
    ) where TCommand : class
        => _handlers.AddHandler<TCommand>(
            new RegisteredHandler<TAggregate, TId>(
                ExpectedState.Unknown,
                (cmd,     _) => new ValueTask<TId>(getId((TCommand)cmd)),
                async (_, cmd, ct) => await action((TCommand)cmd, ct).NoContext(),
                cmd => (resolveStore ?? DefaultResolve<TCommand>())((TCommand)cmd)
            )
        );
}
