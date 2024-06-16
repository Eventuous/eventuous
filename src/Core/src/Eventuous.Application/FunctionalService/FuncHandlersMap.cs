// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Eventuous.Diagnostics;
using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

record RegisteredFuncHandler<T>(
        ExpectedState                   ExpectedState,
        GetStreamNameFromUntypedCommand GetStream,
        ExecuteUntypedCommand<T>        Handler,
        ResolveReaderFromCommand        ResolveReaderFromCommand,
        ResolveWriterFromCommand        ResolveWriterFromCommand
    ) where T : State<T>;

class FuncHandlersMap<TState> where TState : State<TState> {
    readonly TypeMap<RegisteredFuncHandler<TState>> _typeMap = new();

    static readonly MethodInfo AddHandlerInternalMethod =
        typeof(FuncHandlersMap<TState>).GetMethod(nameof(AddHandlerInternal), BindingFlags.NonPublic | BindingFlags.Instance)!;

    internal void AddHandlerUntyped(Type command, RegisteredFuncHandler<TState> handler)
        => AddHandlerInternalMethod.MakeGenericMethod(command).Invoke(this, [handler]);

    void AddHandlerInternal<TCommand>(RegisteredFuncHandler<TState> handler) where TCommand : class {
        try {
            _typeMap.Add<TCommand>(handler);
            ApplicationEventSource.Log.CommandHandlerRegistered<TCommand>();
        } catch (Exceptions.DuplicateTypeException<TCommand>) {
            ApplicationEventSource.Log.CommandHandlerAlreadyRegistered<TCommand>();

            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredFuncHandler<TState>? handler) => _typeMap.TryGetValue<TCommand>(out handler);
}
