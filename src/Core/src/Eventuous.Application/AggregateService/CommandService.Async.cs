// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

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
            Func<TCommand, TId>                                 getId,
            Func<TAggregate, TCommand, CancellationToken, Task> action,
            Func<TCommand, IEventStore>?                        resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.New).GetId(getId).ResolveStore(resolveStore).ActAsync(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.New).GetId(getId).ActAsync(action);
        }
    }

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
            Func<TCommand, TId>                                 getId,
            Func<TAggregate, TCommand, CancellationToken, Task> action,
            Func<TCommand, IEventStore>?                        resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.Existing).GetId(getId).ResolveStore(resolveStore).ActAsync(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.Existing).GetId(getId).ActAsync(action);
        }
    }

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
            Func<TCommand, CancellationToken, ValueTask<TId>>   getId,
            Func<TAggregate, TCommand, CancellationToken, Task> action,
            Func<TCommand, IEventStore>?                        resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.Existing).GetIdAsync(getId).ResolveStore(resolveStore).ActAsync(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.Existing).GetIdAsync(getId).ActAsync(action);
        }
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetId(...).ActAsync(...).ResolveStore(...) instead")]
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(
            Func<TCommand, TId>                                 getId,
            Func<TAggregate, TCommand, CancellationToken, Task> action,
            Func<TCommand, IEventStore>?                        resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.Any).GetId(getId).ResolveStore(resolveStore).ActAsync(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.Any).GetId(getId).ActAsync(action);
        }
    }

    /// <summary>
    /// Register an asynchronous handler for a command, which is expected to use a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">Asynchronous function to get the aggregate id from the command</param>
    /// <param name="action">Asynchronous action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetIdAsync(...).ActAsync(...).ResolveStore(...) instead")]
    [PublicAPI]
    protected void OnAnyAsync<TCommand>(
            Func<TCommand, CancellationToken, ValueTask<TId>>   getId,
            Func<TAggregate, TCommand, CancellationToken, Task> action,
            Func<TCommand, IEventStore>?                        resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.Any).GetIdAsync(getId).ResolveStore(resolveStore).ActAsync(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.Any).GetIdAsync(getId).ActAsync(action);
        }
    }
}
