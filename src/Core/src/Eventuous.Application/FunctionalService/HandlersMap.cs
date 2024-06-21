// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Eventuous.Diagnostics;
using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

record RegisteredHandler<T>(
        ExpectedState                   ExpectedState,
        GetStreamNameFromUntypedCommand GetStream,
        ExecuteUntypedCommand<T>        Handler,
        ResolveReaderFromCommand        ResolveReaderFromCommand,
        ResolveWriterFromCommand        ResolveWriterFromCommand
    ) where T : State<T>;

class HandlersMap<TState> where TState : State<TState> {
    readonly TypeMap<RegisteredHandler<TState>> _typeMap = new();

    static readonly MethodInfo AddHandlerInternalMethod =
        typeof(HandlersMap<TState>).GetMethod(nameof(AddHandlerInternal), BindingFlags.NonPublic | BindingFlags.Instance)!;

    internal void AddHandlerUntyped(Type commandType, RegisteredHandler<TState> handler)
        => AddHandlerInternalMethod.MakeGenericMethod(commandType).Invoke(this, [handler]);

    void AddHandlerInternal<TCommand>(RegisteredHandler<TState> handler) where TCommand : class {
        try {
            _typeMap.Add<TCommand>(handler);
            ApplicationEventSource.Log.CommandHandlerRegistered<TCommand>();
        } catch (Exceptions.DuplicateTypeException<TCommand>) {
            ApplicationEventSource.Log.CommandHandlerAlreadyRegistered<TCommand>();

            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredHandler<TState>? handler) => _typeMap.TryGetValue<TCommand>(out handler);
}
