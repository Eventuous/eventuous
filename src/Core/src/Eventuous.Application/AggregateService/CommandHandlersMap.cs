// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using static Eventuous.CommandServiceDelegates;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

record RegisteredHandler<T, TState, TId>(
        ExpectedState                   ExpectedState,
        GetIdFromUntypedCommand<TId>    GetId,
        HandleUntypedCommand<T, TState> Handler,
        ResolveStoreFromCommand         ResolveStore
    ) where T : Aggregate<TState> where TId : Id where TState : State<TState>, new();

class HandlersMap<TAggregate, TState, TId> where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new() {
    readonly TypeMap<RegisteredHandler<TAggregate, TState, TId>> _typeMap = new();

    static readonly MethodInfo AddHandlerInternalMethod =
        typeof(HandlersMap<TAggregate, TState, TId>).GetMethod(nameof(AddHandlerInternal), BindingFlags.NonPublic | BindingFlags.Instance)!;

    internal void AddHandlerInternal<TCommand>(RegisteredHandler<TAggregate, TState, TId> handler) {
        try {
            _typeMap.Add<TCommand>(handler);
            Log.CommandHandlerRegistered<TCommand>();
        } catch (Exceptions.DuplicateTypeException<TCommand>) {
            Log.CommandHandlerAlreadyRegistered<TCommand>();

            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    internal void AddHandlerUntyped(Type command, RegisteredHandler<TAggregate, TState, TId> handler)
        => AddHandlerInternalMethod.MakeGenericMethod(command).Invoke(this, [handler]);

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredHandler<TAggregate, TState, TId>? handler) => _typeMap.TryGetValue<TCommand>(out handler);
}
