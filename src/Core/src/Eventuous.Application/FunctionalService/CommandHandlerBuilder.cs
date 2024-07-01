// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

public interface IDefineExpectedState<out TCommand, out TState> where TState : State<TState> where TCommand : class {
    /// <summary>
    /// Defines the expected stream state for handling the command.
    /// </summary>
    /// <param name="expectedState">Expected stream state</param>
    /// <returns></returns>
    IDefineStreamName<TCommand, TState> InState(ExpectedState expectedState);
}

public interface IDefineStreamName<out TCommand, out TState> where TState : State<TState> where TCommand : class {
    /// <summary>
    /// Defines how to get the stream name from the command.
    /// </summary>
    /// <param name="getStream">A function to get the stream name from the command</param>
    /// <returns></returns>
    ICommandHandlerBuilder<TCommand, TState> GetStream(Func<TCommand, StreamName> getStream);

    /// <summary>
    /// Defines how to get the stream name from the command, asynchronously.
    /// </summary>
    /// <param name="getStream">A function to get the stream name from the command</param>
    /// <returns></returns>
    ICommandHandlerBuilder<TCommand, TState> GetStreamAsync(Func<TCommand, CancellationToken, ValueTask<StreamName>> getStream);
}

public interface IDefineStore<out TCommand, out TState> where TState : State<TState> where TCommand : class {
    /// <summary>
    /// Defines how to resolve the event store from the command. It assigns both reader and writer.
    /// If not defined, the reader and writer provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveStore">Function to resolve the event writer</param>
    /// <returns></returns>
    IDefineExecution<TCommand, TState> ResolveStore(Func<TCommand, IEventStore> resolveStore);
}

public interface IDefineReader<out TCommand, out TState> where TState : State<TState> where TCommand : class {
    /// <summary>
    /// Defines how to resolve the event reader from the command.
    /// If not defined, the reader provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveReader">Function to resolve the event reader</param>
    /// <returns></returns>
    IDefineWriter<TCommand, TState> ResolveReader(Func<TCommand, IEventReader> resolveReader);
}

public interface IDefineWriter<out TCommand, out TState> where TState : State<TState> where TCommand : class {
    /// <summary>
    /// Defines how to resolve the event writer from the command.
    /// If not defined, the writer provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveWriter">Function to resolve the event writer</param>
    /// <returns></returns>
    IDefineExecution<TCommand, TState> ResolveWriter(Func<TCommand, IEventWriter> resolveWriter);
}

public interface IDefineExecution<out TCommand, out TState> where TState : State<TState> where TCommand : class {
    /// <summary>
    /// Defines the action to take on the stream for the command.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on the stream for the command</param>
    /// <returns></returns>
    void Act(Func<TCommand, IEnumerable<object>> executeCommand);

    /// <summary>
    /// Defines the action to take on the stream for the command, asynchronously.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on the stream for the command</param>
    /// <returns></returns>
    void ActAsync(Func<TCommand, CancellationToken, Task<IEnumerable<object>>> executeCommand);

    /// <summary>
    /// Defines the action to take on the new stream for the command.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on a new stream for the command</param>
    /// <returns></returns>
    void Act(Func<TState, object[], TCommand, IEnumerable<object>> executeCommand);

    /// <summary>
    /// Defines the action to take on the new stream for the command, asynchronously.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on a new stream for the command</param>
    /// <returns></returns>
    void ActAsync(Func<TState, object[], TCommand, CancellationToken, Task<IEnumerable<object>>> executeCommand);
}

public interface ICommandHandlerBuilder<out TCommand, out TState>
    : IDefineStore<TCommand, TState>, IDefineReader<TCommand, TState>, IDefineWriter<TCommand, TState>, IDefineExecution<TCommand, TState>
    where TState : State<TState> where TCommand : class;

public class CommandHandlerBuilder<TCommand, TState>(CommandService<TState> service, IEventReader? reader, IEventWriter? writer)
    : IDefineExpectedState<TCommand, TState>, IDefineStreamName<TCommand, TState>, ICommandHandlerBuilder<TCommand, TState>
    where TState : State<TState>, new() where TCommand : class {
    ExpectedState                    _expectedState = ExpectedState.Any;
    GetStreamNameFromUntypedCommand? _getStream;
    ExecuteUntypedCommand<TState>?   _execute;
    Func<TCommand, IEventReader>?    _reader;
    Func<TCommand, IEventWriter>?    _writer;

    IDefineStreamName<TCommand, TState> IDefineExpectedState<TCommand, TState>.InState(ExpectedState expectedState) {
        _expectedState = expectedState;

        return this;
    }

    ICommandHandlerBuilder<TCommand, TState> IDefineStreamName<TCommand, TState>.GetStream(Func<TCommand, StreamName> getStream) {
        _getStream = (cmd, _) => ValueTask.FromResult(getStream((TCommand)cmd));

        return this;
    }

    ICommandHandlerBuilder<TCommand, TState> IDefineStreamName<TCommand, TState>.GetStreamAsync(
            Func<TCommand, CancellationToken, ValueTask<StreamName>> getStream
        ) {
        _getStream = (cmd, token) => getStream((TCommand)cmd, token);

        return this;
    }

    void IDefineExecution<TCommand, TState>.Act(Func<TState, object[], TCommand, IEnumerable<object>> executeCommand) {
        _execute = (state, events, command, _) => ValueTask.FromResult(executeCommand(state, events, (TCommand)command));
        service.AddHandler<TCommand>(Build());
    }

    void IDefineExecution<TCommand, TState>.ActAsync(Func<TState, object[], TCommand, CancellationToken, Task<IEnumerable<object>>> executeCommand) {
        _execute = async (state, events, cmd, token) => await executeCommand(state, events, (TCommand)cmd, token).NoContext();
        service.AddHandler<TCommand>(Build());
    }

    void IDefineExecution<TCommand, TState>.Act(Func<TCommand, IEnumerable<object>> executeCommand) {
        // This is not ideal as we can return more specific interface depending on the expected state, but it would do for now.
        if (_expectedState != ExpectedState.New) {
            throw new InvalidOperationException("Action without state is only allowed for new streams");
        }

        _execute = (_, _, command, _) => ValueTask.FromResult(executeCommand((TCommand)command));
        service.AddHandler<TCommand>(Build());
    }

    void IDefineExecution<TCommand, TState>.ActAsync(Func<TCommand, CancellationToken, Task<IEnumerable<object>>> executeCommand) {
        // This is not ideal as we can return more specific interface depending on the expected state, but it would do for now.
        if (_expectedState != ExpectedState.New) {
            throw new InvalidOperationException("Action without state is only allowed for new streams");
        }

        _execute = executeCommand.AsExecute<TCommand, TState>();
        service.AddHandler<TCommand>(Build());
    }

    IDefineWriter<TCommand, TState> IDefineReader<TCommand, TState>.ResolveReader(Func<TCommand, IEventReader> resolveReader) {
        _reader = Ensure.NotNull(resolveReader);

        return this;
    }

    IDefineExecution<TCommand, TState> IDefineWriter<TCommand, TState>.ResolveWriter(Func<TCommand, IEventWriter> resolveWriter) {
        _writer = Ensure.NotNull(resolveWriter);

        return this;
    }

    IDefineExecution<TCommand, TState> IDefineStore<TCommand, TState>.ResolveStore(Func<TCommand, IEventStore> resolveStore) {
        Ensure.NotNull(resolveStore);
        _reader ??= resolveStore;
        _writer ??= resolveStore;

        return this;
    }

    RegisteredHandler<TState> Build() {
        return new(
            _expectedState,
            Ensure.NotNull(_getStream, $"Function to get the stream id from {typeof(TCommand).Name} is not defined"),
            Ensure.NotNull(_execute, $"Function to act on the stream for command {typeof(TCommand).Name} is not defined"),
            (_reader ?? DefaultResolveReader()).AsResolveReader(),
            (_writer ?? DefaultResolveWriter()).AsResolveWriter()
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
