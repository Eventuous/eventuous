// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

public delegate Task ActOnAggregateAsync<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command, CancellationToken cancellationToken)
    where TAggregate : Aggregate;

public delegate void ActOnAggregate<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command) where TAggregate : Aggregate;

delegate ValueTask<T> HandleUntypedCommand<T>(T aggregate, object command, CancellationToken cancellationToken) where T : Aggregate;

public delegate Task<TId> GetIdFromCommandAsync<TId, in TCommand>(TCommand command, CancellationToken cancellationToken) where TId : Id where TCommand : class;

public delegate TId GetIdFromCommand<out TId, in TCommand>(TCommand command) where TId : Id where TCommand : class;

delegate ValueTask<TId> GetIdFromUntypedCommand<TId>(object command, CancellationToken cancellationToken) where TId : Id;

public delegate IAggregateStore ResolveStore<in TCommand>(TCommand command) where TCommand : class;

delegate IAggregateStore ResolveStoreFromCommand(object command);

record RegisteredHandler<T, TId>(
        ExpectedState                ExpectedState,
        GetIdFromUntypedCommand<TId> GetId,
        HandleUntypedCommand<T>      Handler,
        ResolveStoreFromCommand      ResolveStore
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
