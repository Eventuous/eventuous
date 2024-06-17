// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.CommandServiceDelegates;

namespace Eventuous;

public abstract class CommandHandlerBuilder<TAggregate, TState, TId> where TAggregate : Aggregate<TState> where TState : State<TState>, new() where TId : Id {
    internal abstract RegisteredHandler<TAggregate, TState, TId> Build();
}

/// <summary>
/// Builds a command handler for a specific command type. You would not need to instantiate this class directly,
/// use <see cref="CommandService{TAggregate,TState,TId}.On{TCommand}" /> function.
/// </summary>
/// <param name="store">Default aggregate store instance for the command service</param>
/// <typeparam name="TCommand">Command type</typeparam>
/// <typeparam name="TAggregate">Aggregate type</typeparam>
/// <typeparam name="TState">State of the aggregate type</typeparam>
/// <typeparam name="TId">Identity of the aggregate type</typeparam>
public class CommandHandlerBuilder<TCommand, TAggregate, TState, TId>(IAggregateStore? store) : CommandHandlerBuilder<TAggregate, TState, TId>
    where TCommand : class
    where TAggregate : Aggregate<TState>, new()
    where TState : State<TState>, new()
    where TId : Id {
    GetIdFromUntypedCommand<TId>?             _getId;
    HandleUntypedCommand<TAggregate, TState>? _action;
    ResolveStore<TCommand>?                   _resolveStore;
    ExpectedState                             _expectedState = ExpectedState.Any;

    /// <summary>
    /// Set the expected aggregate state for the command handler.
    /// If the aggregate isn't in the expected state, the command handler will return an error.
    /// The default is <see cref="ExpectedState.Any" />.
    /// </summary>
    /// <param name="expectedState">Expected aggregate state</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> InState(ExpectedState expectedState) {
        _expectedState = expectedState;

        return this;
    }

    /// <summary>
    /// Defines how the aggregate id is extracted from the command.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command.</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> GetId(GetIdFromCommand<TId, TCommand> getId) {
        _getId = getId.AsGetId();

        return this;
    }

    /// <summary>
    /// Defines how the aggregate id is extracted from the command, asynchronously.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command.</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> GetIdAsync(GetIdFromCommandAsync<TId, TCommand> getId) {
        _getId = getId.AsGetId();

        return this;
    }

    /// <summary>
    /// Defines how the aggregate is acted upon by the command.
    /// </summary>
    /// <param name="action">A function that executes an operation on an aggregate</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> Act(ActOnAggregate<TAggregate, TState, TCommand> action) {
        _action = action.AsAct();

        return this;
    }

    /// <summary>
    /// Defines how the aggregate is acted upon by the command, asynchronously.
    /// </summary>
    /// <param name="action">A function that executes an asynchronous operation on an aggregate</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> ActAsync(ActOnAggregateAsync<TAggregate, TState, TCommand> action) {
        _action = action.AsAct();

        return this;
    }

    /// <summary>
    /// Defines how the aggregate store is resolved from the command. It is optional. If not defined, the default
    /// aggregate store of the command service will be used.
    /// </summary>
    /// <param name="resolveStore"></param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> ResolveStore(ResolveStore<TCommand>? resolveStore) {
        _resolveStore = resolveStore;

        return this;
    }

    internal override RegisteredHandler<TAggregate, TState, TId> Build() {
        return new RegisteredHandler<TAggregate, TState, TId>(
            _expectedState,
            Ensure.NotNull(_getId, $"Function to get the aggregate id from {typeof(TCommand).Name} is not defined"),
            Ensure.NotNull(_action, $"Function to act on the aggregate for command {typeof(TCommand).Name} is not defined"),
            (_resolveStore ?? DefaultResolve()).AsResolveStore()
        );
    }

    ResolveStore<TCommand> DefaultResolve() {
        ArgumentNullException.ThrowIfNull(store, nameof(store));

        return _ => store;
    }
}
