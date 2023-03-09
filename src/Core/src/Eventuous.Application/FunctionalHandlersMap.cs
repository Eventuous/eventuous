// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using static Eventuous.Diagnostics.ApplicationEventSource;

namespace Eventuous;

delegate ValueTask<IEnumerable<object>> ExecuteUntypedCommand<T>(
    T                 state,
    object[]          events,
    object            command,
    CancellationToken cancellationToken
) where T : State<T>;

record RegisteredFuncHandler<T>(ExpectedState ExpectedState, ExecuteUntypedCommand<T> Handler) where T : State<T>;

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

    // public void AddHandler<TCommand>(ExpectedState expectedState, ExecuteCommand<T, TCommand> action) where TCommand : class
    //     => AddHandler<TCommand>(
    // new RegisteredHandler<T>(
    // expectedState,
    // async (aggregate, cmd, ct) => {
    //     await action(aggregate, (TCommand)cmd, ct).NoContext();
    //     return aggregate;
    // }
    // )
    // );

    public void AddHandler<TCommand>(ExpectedState expectedState, ExecuteCommand<T, TCommand> action) where TCommand : class {
        ValueTask<IEnumerable<object>> Handler(T state, object[] events, object command, CancellationToken token) {
            var newEvents = action(state, events, (TCommand)command);
            return new ValueTask<IEnumerable<object>>(newEvents);
        }

        AddHandler<TCommand>(new RegisteredFuncHandler<T>(expectedState, Handler));
    }

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredFuncHandler<T>? handler)
        => _typeMap.TryGetValue<TCommand>(out handler);
}
