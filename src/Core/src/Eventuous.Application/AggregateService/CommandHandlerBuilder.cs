// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.CommandServiceDelegates;

namespace Eventuous;

public interface IDefineExpectedState<out TCommand, out TAggregate, out TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class {
    /// <summary>
    /// Defines the expected stream state for handling the command.
    /// </summary>
    /// <param name="expectedState">Expected stream state</param>
    /// <returns></returns>
    IDefineIdentity<TCommand, TAggregate, TState, TId> InState(ExpectedState expectedState);
}

public interface IDefineIdentity<out TCommand, out TAggregate, out TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class {
    /// <summary>
    /// Defines how the aggregate id is extracted from the command.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command.</param>
    /// <returns></returns>
    ICommandHandlerBuilder<TCommand, TAggregate, TState, TId> GetId(Func<TCommand, TId> getId);

    /// <summary>
    /// Defines how the aggregate id is extracted from the command, asynchronously.
    /// </summary>
    /// <param name="getId">A function to get the aggregate id from the command.</param>
    /// <returns></returns>
    ICommandHandlerBuilder<TCommand, TAggregate, TState, TId> GetIdAsync(Func<TCommand, CancellationToken, ValueTask<TId>> getId);
}

public interface IDefineStore<out TCommand, out TAggregate, out TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class {
    /// <summary>
    /// Defines how to resolve the event store from the command. It assigns both reader and writer.
    /// If not defined, the reader and writer provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveStore">Function to resolve the event writer</param>
    /// <returns></returns>
    IDefineExecution<TCommand, TAggregate, TState, TId> ResolveStore(Func<TCommand, IEventStore> resolveStore);
}

public interface IDefineReader<out TCommand, out TAggregate, out TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class {
    /// <summary>
    /// Defines how to resolve the event reader from the command.
    /// If not defined, the reader provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveReader">Function to resolve the event reader</param>
    /// <returns></returns>
    IDefineWriter<TCommand, TAggregate, TState, TId> ResolveReader(Func<TCommand, IEventReader> resolveReader);
}

public interface IDefineWriter<out TCommand, out TAggregate, out TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class {
    /// <summary>
    /// Defines how to resolve the event writer from the command.
    /// If not defined, the writer provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveWriter">Function to resolve the event writer</param>
    /// <returns></returns>
    IDefineExecution<TCommand, TAggregate, TState, TId> ResolveWriter(Func<TCommand, IEventWriter> resolveWriter);
}

public interface IDefineEventAmendment<out TCommand, out TAggregate, out TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class {
    /// <summary>
    /// Amends the event before it gets stored.
    /// </summary>
    /// <param name="amendEvent">A function to amend the event</param>
    /// <returns></returns>
    IDefineStoreOrExecution<TCommand, TAggregate, TState, TId> AmendEvent(AmendEvent<TCommand> amendEvent);
}

public interface IDefineExecution<out TCommand, out TAggregate, out TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class {
    /// <summary>
    /// Defines how the command that acts on the aggregate.
    /// </summary>
    /// <param name="action">A function that executes an operation on an aggregate</param>
    /// <returns></returns>
    void Act(Action<TAggregate, TCommand> action);

    /// <summary>
    /// Defines how the command that acts on the aggregate.
    /// </summary>
    /// <param name="action">A function that executes an asynchronous operation on an aggregate</param>
    /// <returns></returns>
    void ActAsync(Func<TAggregate, TCommand, CancellationToken, Task> action);
}

public interface IDefineStoreOrExecution<out TCommand, out TAggregate, out TState, TId>
    : IDefineStore<TCommand, TAggregate, TState, TId>,
        IDefineReader<TCommand, TAggregate, TState, TId>,
        IDefineWriter<TCommand, TAggregate, TState, TId>,
        IDefineExecution<TCommand, TAggregate, TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class;

public interface ICommandHandlerBuilder<out TCommand, out TAggregate, out TState, TId>
    : IDefineStore<TCommand, TAggregate, TState, TId>,
        IDefineReader<TCommand, TAggregate, TState, TId>,
        IDefineWriter<TCommand, TAggregate, TState, TId>,
        IDefineExecution<TCommand, TAggregate, TState, TId>,
        IDefineEventAmendment<TCommand, TAggregate, TState, TId>
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id
    where TCommand : class;

/// <summary>
/// Builds a command handler for a specific command type. You would not need to instantiate this class directly,
/// use <see cref="CommandService{TAggregate,TState,TId}.On{TCommand}" /> function.
/// </summary>
/// <param name="service">Parent command service</param>
/// <param name="reader">Default event reader instance for the command service</param>
/// <param name="writer">Default event writer instance for the command service</param>
/// <typeparam name="TCommand">Command type</typeparam>
/// <typeparam name="TAggregate">Aggregate type</typeparam>
/// <typeparam name="TState">State of the aggregate type</typeparam>
/// <typeparam name="TId">Identity of the aggregate type</typeparam>
public class CommandHandlerBuilder<TCommand, TAggregate, TState, TId>(
        CommandService<TAggregate, TState, TId> service,
        IEventReader?                           reader,
        IEventWriter?                           writer
    )
    : IDefineExpectedState<TCommand, TAggregate, TState, TId>,
        IDefineIdentity<TCommand, TAggregate, TState, TId>,
        IDefineStoreOrExecution<TCommand, TAggregate, TState, TId>,
        ICommandHandlerBuilder<TCommand, TAggregate, TState, TId>
    where TCommand : class
    where TAggregate : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id {
    GetIdFromUntypedCommand<TId>?             _getId;
    HandleUntypedCommand<TAggregate, TState>? _action;
    Func<TCommand, IEventReader>?             _reader;
    Func<TCommand, IEventWriter>?             _writer;
    AmendEvent<TCommand>?                     _amendEvent;
    ExpectedState                             _expectedState = ExpectedState.Any;

    IDefineIdentity<TCommand, TAggregate, TState, TId> IDefineExpectedState<TCommand, TAggregate, TState, TId>.InState(ExpectedState expectedState) {
        _expectedState = expectedState;

        return this;
    }

    ICommandHandlerBuilder<TCommand, TAggregate, TState, TId> IDefineIdentity<TCommand, TAggregate, TState, TId>.GetId(Func<TCommand, TId> getId) {
        _getId = (cmd, _) => ValueTask.FromResult(getId((TCommand)cmd));

        return this;
    }

    ICommandHandlerBuilder<TCommand, TAggregate, TState, TId> IDefineIdentity<TCommand, TAggregate, TState, TId>.GetIdAsync(Func<TCommand, CancellationToken, ValueTask<TId>> getId) {
        _getId = (cmd, token) => getId((TCommand)cmd, token);

        return this;
    }

    void IDefineExecution<TCommand, TAggregate, TState, TId>.Act(Action<TAggregate, TCommand> action) {
        _action = (aggregate, cmd, _) => {
            action(aggregate, (TCommand)cmd);

            return ValueTask.FromResult(aggregate);
        };
        service.AddHandler<TCommand>(Build());
    }

    void IDefineExecution<TCommand, TAggregate, TState, TId>.ActAsync(Func<TAggregate, TCommand, CancellationToken, Task> action) {
        _action = async (aggregate, cmd, token) => {
            await action(aggregate, (TCommand)cmd, token).NoContext();

            return aggregate;
        };
        service.AddHandler<TCommand>(Build());
    }

    IDefineExecution<TCommand, TAggregate, TState, TId> IDefineStore<TCommand, TAggregate, TState, TId>.ResolveStore(Func<TCommand, IEventStore> resolveStore) {
        Ensure.NotNull(resolveStore, nameof(resolveStore));
        _reader = resolveStore;
        _writer = resolveStore;

        return this;
    }

    IDefineWriter<TCommand, TAggregate, TState, TId> IDefineReader<TCommand, TAggregate, TState, TId>.ResolveReader(Func<TCommand, IEventReader> resolveReader) {
        _reader = resolveReader;

        return this;
    }

    IDefineExecution<TCommand, TAggregate, TState, TId> IDefineWriter<TCommand, TAggregate, TState, TId>.ResolveWriter(Func<TCommand, IEventWriter> resolveWriter) {
        _writer = resolveWriter;

        return this;
    }

    IDefineStoreOrExecution<TCommand, TAggregate, TState, TId> IDefineEventAmendment<TCommand, TAggregate, TState, TId>.AmendEvent(AmendEvent<TCommand> amendEvent) {
        _amendEvent = amendEvent;

        return this;
    }

    RegisteredHandler<TAggregate, TState, TId> Build() {
        return new(
            _expectedState,
            Ensure.NotNull(_getId, $"Function to get the aggregate id from {typeof(TCommand).Name} is not defined"),
            Ensure.NotNull(_action, $"Function to act on the aggregate for command {typeof(TCommand).Name} is not defined"),
            (_reader ?? DefaultResolveReader()).AsResolveReader(),
            (_writer ?? DefaultResolveWriter()).AsResolveWriter(),
            _amendEvent?.AsAmendEvent()
        );

        Func<TCommand, IEventWriter> DefaultResolveWriter()
            => _ => Ensure.NotNull(writer, $"Function to resolve event writer from {typeof(TCommand).Name} is not defined and no default writer is set");

        Func<TCommand, IEventReader> DefaultResolveReader()
            => _ => Ensure.NotNull(reader, $"Function to resolve event reader from {typeof(TCommand).Name} is not defined and no default reader is set");
    }
}
