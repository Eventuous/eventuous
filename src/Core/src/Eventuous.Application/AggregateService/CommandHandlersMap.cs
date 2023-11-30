// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using static Eventuous.CommandServiceDelegates;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

record RegisteredHandler<T, TId>(
        ExpectedState                ExpectedState,
        GetIdFromUntypedCommand<TId> GetId,
        HandleUntypedCommand<T>      Handler,
        ResolveStoreFromCommand      ResolveStore,
        AmendEvent                   AmendEvent
    ) where T : Aggregate where TId : Id;

class HandlersMap<TAggregate, TId> where TAggregate : Aggregate where TId : Id {
    readonly TypeMap<RegisteredHandler<TAggregate, TId>> _typeMap = new();

    static readonly MethodInfo AddHandlerInternalMethod =
        typeof(HandlersMap<TAggregate, TId>).GetMethod(nameof(AddHandlerInternal), BindingFlags.NonPublic | BindingFlags.Instance)!;

    internal void AddHandlerInternal<TCommand>(RegisteredHandler<TAggregate, TId> handler) {
        try {
            _typeMap.Add<TCommand>(handler);
            Log.CommandHandlerRegistered<TCommand>();
        } catch (Exceptions.DuplicateTypeException<TCommand>) {
            Log.CommandHandlerAlreadyRegistered<TCommand>();

            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    internal void AddHandlerUntyped(Type command, RegisteredHandler<TAggregate, TId> handler)
        => AddHandlerInternalMethod.MakeGenericMethod(command).Invoke(this, new object?[] { handler });

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredHandler<TAggregate, TId>? handler) => _typeMap.TryGetValue<TCommand>(out handler);
}
