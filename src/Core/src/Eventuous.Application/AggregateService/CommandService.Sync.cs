// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.CommandServiceDelegates;

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
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>?              resolveStore = null
    ) where TCommand : class
        => On<TCommand>().InState(ExpectedState.New).GetId(getId).Act(action).ResolveStore(resolveStore);

    /// <summary>
    /// Register a handler for a command, which is expected to use an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Existing).GetId(...).Act(...).ResolveStore(...) instead")]
    protected void OnExisting<TCommand>(
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>?              resolveStore = null
    ) where TCommand : class
        => On<TCommand>().InState(ExpectedState.Existing).GetId(getId).Act(action).ResolveStore(resolveStore);

    /// <summary>
    /// Register a handler for a command, which is expected to use an a new or an existing aggregate instance.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command</param>
    /// <param name="action">Action to be performed on the aggregate, given the aggregate instance and the command</param>
    /// <param name="resolveStore">Resolve aggregate store from the command</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetId(...).Act(...).ResolveStore(...) instead")]
    protected void OnAny<TCommand>(
            GetIdFromCommand<TId, TCommand>      getId,
            ActOnAggregate<TAggregate, TCommand> action,
            ResolveStore<TCommand>?              resolveStore = null
        ) where TCommand : class
        => On<TCommand>().InState(ExpectedState.Any).GetId(getId).Act(action).ResolveStore(resolveStore);
}
