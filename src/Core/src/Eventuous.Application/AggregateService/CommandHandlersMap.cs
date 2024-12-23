// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Eventuous.Persistence;
using static Eventuous.CommandServiceDelegates;
using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

record RegisteredHandler<TAggregate, TState, TId>(
        ExpectedState                            ExpectedState,
        GetIdFromUntypedCommand<TId>             GetId,
        HandleUntypedCommand<TAggregate, TState> Handler,
        ResolveReaderFromCommand                 ResolveReader,
        ResolveWriterFromCommand                 ResolveWriter,
        AmendEventFromCommand?                   AmendEvent
    ) where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new() {
    public AmendAppend? AmendAppend { get; set; }
}

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
