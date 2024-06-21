// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

public abstract class CommandHandlerBuilder<TState> where TState : State<TState> {
    internal abstract RegisteredHandler<TState> Build();
}

public class CommandHandlerBuilder<TCommand, TState>(IEventReader? reader, IEventWriter? writer) : CommandHandlerBuilder<TState>
    where TState : State<TState> where TCommand : class {
    ExpectedState                       _expectedState = ExpectedState.Any;
    GetStreamNameFromUntypedCommand?    _getStream;
    ExecuteUntypedCommand<TState>?      _execute;
    ResolveReaderFromCommand<TCommand>? _reader;
    ResolveWriterFromCommand<TCommand>? _writer;

    /// <summary>
    /// Defines the expected stream state for handling the command.
    /// </summary>
    /// <param name="expectedState">Expected stream state</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> InState(ExpectedState expectedState) {
        _expectedState = expectedState;

        return this;
    }

    /// <summary>
    /// Defines how to get the stream name from the command.
    /// </summary>
    /// <param name="getStream">A function to get the stream name from the command</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> GetStream(GetStreamNameFromCommand<TCommand> getStream) {
        _getStream = getStream.AsGetStream();

        return this;
    }

    /// <summary>
    /// Defines how to get the stream name from the command, asynchronously.
    /// </summary>
    /// <param name="getStream">A function to get the stream name from the command</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> GetStreamAsync(GetStreamNameFromCommandAsync<TCommand> getStream) {
        _getStream = getStream.AsGetStream();

        return this;
    }

    /// <summary>
    /// Defines the action to take on the stream for the command.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on the stream for the command</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> Act(ExecuteCommand<TState, TCommand> executeCommand) {
        _execute = executeCommand.AsExecute();

        return this;
    }


    /// <summary>
    /// Defines the action to take on the stream for the command, asynchronously.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on the stream for the command</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> ActAsync(ExecuteCommandAsync<TState, TCommand> executeCommand) {
        _execute = executeCommand.AsExecute();

        return this;
    }

    /// <summary>
    /// Defines the action to take on the new stream for the command.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on a new stream for the command</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> Act(Func<TCommand, IEnumerable<object>> executeCommand) {
        // This is not ideal as we can return more specific interface depending on expected state, but it would do for now.
        if (_expectedState != ExpectedState.New) {
            throw new InvalidOperationException("Action without state is only allowed for new streams");
        }

        _execute = executeCommand.AsExecute<TCommand, TState>();

        return this;
    }

    /// <summary>
    /// Defines the action to take on the new stream for the command, asynchronously.
    /// </summary>
    /// <param name="executeCommand">Function to be executed on a new stream for the command</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> ActAsync(Func<TCommand, Task<IEnumerable<object>>> executeCommand) {
        // This is not ideal as we can return more specific interface depending on expected state, but it would do for now.
        if (_expectedState != ExpectedState.New) {
            throw new InvalidOperationException("Action without state is only allowed for new streams");
        }

        _execute = executeCommand.AsExecute<TCommand, TState>();

        return this;
    }

    /// <summary>
    /// Defines how to resolve the event reader from the command.
    /// If not defined, the reader provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveReader">Function to resolve the event reader</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> ResolveReader(ResolveReaderFromCommand<TCommand>? resolveReader) {
        _reader = resolveReader;

        return this;
    }

    /// <summary>
    /// Defines how to resolve the event writer from the command.
    /// If not defined, the writer provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveWriter">Function to resolve the event writer</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> ResolveWriter(ResolveWriterFromCommand<TCommand>? resolveWriter) {
        _writer = resolveWriter;

        return this;
    }

    /// <summary>
    /// Defines how to resolve the event store from the command. It assigns both reader and writer.
    /// If not defined, the reader and writer provided by the functional service will be used.
    /// </summary>
    /// <param name="resolveStore">Function to resolve the event writer</param>
    /// <returns></returns>
    public CommandHandlerBuilder<TCommand, TState> ResolveStore(ResolveEventStoreFromCommand<TCommand>? resolveStore) {
        _reader ??= resolveStore?.AsResolveReader();
        _writer ??= resolveStore?.AsResolveWriter();

        return this;
    }

    internal override RegisteredHandler<TState> Build() {
        return new(
            _expectedState,
            Ensure.NotNull(_getStream, $"Function to get the stream id from {typeof(TCommand).Name} is not defined"),
            Ensure.NotNull(_execute, $"Function to act on the stream for command {typeof(TCommand).Name} is not defined"),
            (_reader ?? DefaultResolveReader()).AsResolveReader(),
            (_writer ?? DefaultResolveWriter()).AsResolveWriter()
        );

        ResolveWriterFromCommand<TCommand> DefaultResolveWriter() {
            ArgumentNullException.ThrowIfNull(writer, nameof(writer));

            return _ => writer;
        }

        ResolveReaderFromCommand<TCommand> DefaultResolveReader() {
            ArgumentNullException.ThrowIfNull(reader, nameof(reader));

            return _ => reader;
        }
    }
}
