// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

delegate ValueTask<IEnumerable<object>> ExecuteUntypedCommand<in T>(T state, object[] events, object command, CancellationToken cancellationToken) where T : State<T>;

public delegate StreamName GetStreamNameFromCommand<in TCommand>(TCommand command) where TCommand : class;

delegate ValueTask<StreamName> GetStreamNameFromUntypedCommand(object command, CancellationToken cancellationToken);

record RegisteredFuncHandler<T>(
    ExpectedState                   ExpectedState,
    GetStreamNameFromUntypedCommand GetStream,
    ExecuteUntypedCommand<T>        Handler
) where T : State<T>;

class FunctionalHandlersMap<T> where T : State<T> {
    readonly TypeMap<RegisteredFuncHandler<T>> _typeMap = new();

    public void AddHandler<TCommand>(RegisteredFuncHandler<T> handler) where TCommand : class {
        try {
            _typeMap.Add<TCommand>(handler);
            Log.CommandHandlerRegistered<TCommand>();
        }
        catch (Exceptions.DuplicateTypeException<TCommand>) {
            Log.CommandHandlerAlreadyRegistered<TCommand>();
            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    public void AddHandler<TCommand>(ExpectedState expectedState, GetStreamNameFromCommand<TCommand> getStreamName, ExecuteCommand<T, TCommand> action) where TCommand : class {
        ValueTask<IEnumerable<object>> Handler(T state, object[] events, object command, CancellationToken token) => new(action(state, events, (TCommand)command));

        ValueTask<StreamName> GetStream(object cmd, CancellationToken _) => ValueTask.FromResult(getStreamName((TCommand)cmd));

        AddHandler<TCommand>(new RegisteredFuncHandler<T>(expectedState, GetStream, Handler));
    }

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredFuncHandler<T>? handler) => _typeMap.TryGetValue<TCommand>(out handler);
}
