// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.CommandServiceDelegates;

namespace Eventuous;

public abstract class CommandHandlerBuilder<TAggregate, TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id {
    internal abstract RegisteredHandler<TAggregate, TState, TId> Build();
}

/// <summary>
/// Builds a command handler for a specific command type. You would not need to instantiate this class directly,
/// use <see cref="CommandService{TAggregate,TState,TId}.On{TCommand}" /> function.
/// </summary>
/// <param name="reader">Default event reader instance for the command service</param>
/// <param name="writer">Default event writer instance for the command service</param>
/// <typeparam name="TCommand">Command type</typeparam>
/// <typeparam name="TAggregate">Aggregate type</typeparam>
/// <typeparam name="TState">State of the aggregate type</typeparam>
/// <typeparam name="TId">Identity of the aggregate type</typeparam>
public class CommandHandlerBuilder<TCommand, TAggregate, TState, TId>(IEventReader? reader, IEventWriter? writer)
    : CommandHandlerBuilder<TAggregate, TState, TId>
    where TCommand : class
    where TAggregate : Aggregate<TState>, new()
    where TState : State<TState>, new()
    where TId : Id {
    GetIdFromUntypedCommand<TId>?             _getId;
    HandleUntypedCommand<TAggregate, TState>? _action;
    Func<TCommand, IEventReader>?             _reader;
    Func<TCommand, IEventWriter>?             _writer;
    AmendEvent<TCommand>?                     _amendEvent;
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
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> GetId(Func<TCommand, TId> getId) {
        _getId = (cmd, _) => ValueTask.FromResult(getId((TCommand)cmd));

        return this;
    }

    /// <summary>
    /// Defines how the aggregate id is extracted from the command, asynchronously.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command.</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> GetIdAsync(Func<TCommand, CancellationToken, ValueTask<TId>> getId) {
        _getId = (cmd, token) => getId((TCommand)cmd, token);

        return this;
    }

    /// <summary>
    /// Defines how the aggregate is acted upon by the command.
    /// </summary>
    /// <param name="action">A function that executes an operation on an aggregate</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> Act(Action<TAggregate, TCommand> action) {
        _action = (aggregate, cmd, _) => {
            action(aggregate, (TCommand)cmd);

            return ValueTask.FromResult(aggregate);
        };

        return this;
    }

    /// <summary>
    /// Defines how the aggregate is acted upon by the command, asynchronously.
    /// </summary>
    /// <param name="action">A function that executes an asynchronous operation on an aggregate</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> ActAsync(Func<TAggregate, TCommand, CancellationToken, Task> action) {
        _action = async (aggregate, cmd, token) => {
            await action(aggregate, (TCommand)cmd, token);

            return aggregate;
        };

        return this;
    }

    /// <summary>
    /// Defines how the aggregate store is resolved from the command. It is optional. If not defined, the default
    /// aggregate store of the command service will be used.
    /// </summary>
    /// <param name="resolveStore"></param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> ResolveStore(Func<TCommand, IEventStore> resolveStore) {
        Ensure.NotNull(resolveStore, nameof(resolveStore));
        _reader ??= resolveStore;
        _writer ??= resolveStore;

        return this;
    }

    public CommandHandlerBuilder<TCommand, TAggregate, TState, TId> AmendEvent(AmendEvent<TCommand> amendEvent) {
        _amendEvent = amendEvent;

        return this;
    }

    internal override RegisteredHandler<TAggregate, TState, TId> Build() {
        return new(
            _expectedState,
            Ensure.NotNull(_getId, $"Function to get the aggregate id from {typeof(TCommand).Name} is not defined"),
            Ensure.NotNull(_action, $"Function to act on the aggregate for command {typeof(TCommand).Name} is not defined"),
            (_reader ?? DefaultResolveReader()).AsResolveReader(),
            (_writer ?? DefaultResolveWriter()).AsResolveWriter(),
            _amendEvent?.AsAmendEvent()
        );

        Func<TCommand, IEventWriter> DefaultResolveWriter() {
            ArgumentNullException.ThrowIfNull(writer, nameof(writer));

            return _ => writer;
        }

        Func<TCommand, IEventReader> DefaultResolveReader() {
            ArgumentNullException.ThrowIfNull(reader, nameof(reader));

            return _ => reader;
        }
    }
}
