// Copyright (C) Eventuous HQ OÜ. All rights reserved
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
    [Obsolete("Use On<TCommand>().InState(ExpectedState.New).GetId(...).Act(...).ResolveStore(...) instead")]
    protected void OnNew<TCommand>(
            Func<TCommand, TId>          getId,
            Action<TAggregate, TCommand> action,
            Func<TCommand, IEventStore>? resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.New).GetId(getId).ResolveStore(resolveStore).Act(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.New).GetId(getId).Act(action);
        }
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Existing).GetId(...).Act(...).ResolveStore(...) instead")]
    protected void OnExisting<TCommand>(
            Func<TCommand, TId>          getId,
            Action<TAggregate, TCommand> action,
            Func<TCommand, IEventStore>? resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.Existing).GetId(getId).ResolveStore(resolveStore).Act(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.Existing).GetId(getId).Act(action);
        }
    }

    /// <summary>
    /// Register a handler for a command, which is expected to use a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetId(...).Act(...).ResolveStore(...) instead")]
    protected void OnAny<TCommand>(
            Func<TCommand, TId>          getId,
            Action<TAggregate, TCommand> action,
            Func<TCommand, IEventStore>? resolveStore = null
        ) where TCommand : class {
        if (resolveStore != null) {
            On<TCommand>().InState(ExpectedState.Any).GetId(getId).ResolveStore(resolveStore).Act(action);
        }
        else {
            On<TCommand>().InState(ExpectedState.Any).GetId(getId).Act(action);
        }
    }
}
