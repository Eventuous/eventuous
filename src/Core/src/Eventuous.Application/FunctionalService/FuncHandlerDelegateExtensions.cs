// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static partial class FuncServiceDelegates {
    internal static GetStreamNameFromUntypedCommand AsGetStream<TCommand>(this GetStreamNameFromCommand<TCommand> getStream) where TCommand : class
        => (cmd, _) => ValueTask.FromResult(getStream((TCommand)cmd));

    internal static GetStreamNameFromUntypedCommand AsGetStream<TCommand>(this GetStreamNameFromCommandAsync<TCommand> getStream) where TCommand : class
        => async (cmd, token) => await getStream((TCommand)cmd, token);

    internal static ExecuteUntypedCommand<TState> AsExecute<TCommand, TState>(this ExecuteCommand<TState, TCommand> execute)
        where TState : State<TState> where TCommand : class
        => (state, events, command, _) => ValueTask.FromResult(execute(state, events, (TCommand)command));

    internal static ExecuteUntypedCommand<TState> AsExecute<TCommand, TState>(this Func<TCommand, IEnumerable<object>> execute)
        where TState : State<TState> where TCommand : class
        => (_, _, command, _) => ValueTask.FromResult(execute((TCommand)command));

    internal static ExecuteUntypedCommand<TState> AsExecute<TCommand, TState>(this Func<TCommand, Task<IEnumerable<object>>> execute)
        where TState : State<TState> where TCommand : class
        => async (_, _, command, _) => await execute((TCommand)command);

    internal static ExecuteUntypedCommand<TState> AsExecute<TCommand, TState>(this ExecuteCommandAsync<TState, TCommand> execute)
        where TState : State<TState> where TCommand : class
        => async (state, events, command, token) => await execute(state, events, (TCommand)command, token);

    internal static ResolveWriterFromCommand<TCommand> AsResolveWriter<TCommand>(this ResolveEventStoreFromCommand<TCommand> resolveStore) where TCommand : class
        => cmd => resolveStore(cmd);

    internal static ResolveReaderFromCommand<TCommand> AsResolveReader<TCommand>(this ResolveEventStoreFromCommand<TCommand> resolveStore) where TCommand : class
        => cmd => resolveStore(cmd);

    internal static ResolveReaderFromCommand AsResolveReader<TCommand>(this ResolveReaderFromCommand<TCommand> resolveReader) where TCommand : class
        => cmd => resolveReader((TCommand)cmd);

    internal static ResolveWriterFromCommand AsResolveWriter<TCommand>(this ResolveWriterFromCommand<TCommand> resolveWriter) where TCommand : class
        => cmd => resolveWriter((TCommand)cmd);
}
