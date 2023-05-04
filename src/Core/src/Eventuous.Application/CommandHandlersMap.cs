// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

public delegate Task ActOnAggregateAsync<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command, CancellationToken cancellationToken) where TAggregate : Aggregate;

public delegate void ActOnAggregate<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command) where TAggregate : Aggregate;

delegate ValueTask<T> HandleUntypedCommand<T>(T aggregate, object command, CancellationToken cancellationToken) where T : Aggregate;

public delegate Task<TId> GetIdFromCommandAsync<TId, in TCommand>(TCommand command, CancellationToken cancellationToken) where TId : Id where TCommand : class;

public delegate TId GetIdFromCommand<out TId, in TCommand>(TCommand command) where TId : Id where TCommand : class;

delegate ValueTask<TId> GetIdFromUntypedCommand<TId>(object command, CancellationToken cancellationToken) where TId : Id;

delegate IAggregateStore ResolveStoreFromCommand(object command);

record RegisteredHandler<T, TId>(
    ExpectedState                ExpectedState,
    GetIdFromUntypedCommand<TId> GetId,
    HandleUntypedCommand<T>      Handler,
    ResolveStoreFromCommand      ResolveStore
) where T : Aggregate where TId : Id;

class HandlersMap<TAggregate, TId> where TAggregate : Aggregate where TId : Id {
    readonly TypeMap<RegisteredHandler<TAggregate, TId>> _typeMap = new();

    public void AddHandler<TCommand>(RegisteredHandler<TAggregate, TId> handler) {
        try {
            _typeMap.Add<TCommand>(handler);
            Log.CommandHandlerRegistered<TCommand>();
        }
        catch (Exceptions.DuplicateTypeException<TCommand>) {
            Log.CommandHandlerAlreadyRegistered<TCommand>();
            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    public void AddHandler<TCommand>(
        ExpectedState                             expectedState,
        GetIdFromCommand<TId, TCommand>           getId,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        ResolveStore<TCommand>                    resolveStore
    ) where TCommand : class
        => AddHandler<TCommand>(new RegisteredHandler<TAggregate, TId>(expectedState, getId.AsGetId(), action.AsAct(), resolveStore.AsResolveStore()));

    public void AddHandler<TCommand>(
        ExpectedState                        expectedState,
        GetIdFromCommand<TId, TCommand>      getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>               resolveStore
    ) where TCommand : class
        => AddHandler<TCommand>(new RegisteredHandler<TAggregate, TId>(expectedState, getId.AsGetId(), action.AsAct(), resolveStore.AsResolveStore()));

    public void AddHandler<TCommand>(
        ExpectedState                             expectedState,
        GetIdFromCommandAsync<TId, TCommand>      getId,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        ResolveStore<TCommand>                    resolveStore
    ) where TCommand : class
        => AddHandler<TCommand>(new RegisteredHandler<TAggregate, TId>(expectedState, getId.AsGetId(), action.AsAct(), resolveStore.AsResolveStore()));

    public void AddHandler<TCommand>(
        ExpectedState                        expectedState,
        GetIdFromCommandAsync<TId, TCommand> getId,
        ActOnAggregate<TAggregate, TCommand> action,
        ResolveStore<TCommand>               resolveStore
    ) where TCommand : class
        => AddHandler<TCommand>(new RegisteredHandler<TAggregate, TId>(expectedState, getId.AsGetId(), action.AsAct(), resolveStore.AsResolveStore()));

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredHandler<TAggregate, TId>? handler) => _typeMap.TryGetValue<TCommand>(out handler);
}

public delegate IEnumerable<object> ExecuteCommand<in T, in TCommand>(T state, object[] originalEvents, TCommand command)
    where T : State<T> where TCommand : class;

static class CommandHandlingDelegateExtensions {
    public static GetIdFromUntypedCommand<TId> AsGetId<TId, TCommand>(this GetIdFromCommandAsync<TId, TCommand> getId) where TId : Id where TCommand : class
        => async (cmd, ct) => await getId((TCommand)cmd, ct);

    public static GetIdFromUntypedCommand<TId> AsGetId<TId, TCommand>(this GetIdFromCommand<TId, TCommand> getId) where TId : Id where TCommand : class
        => (cmd, _) => ValueTask.FromResult(getId((TCommand)cmd));

    public static HandleUntypedCommand<TAggregate> AsAct<TAggregate, TCommand>(this ActOnAggregateAsync<TAggregate, TCommand> act) where TAggregate : Aggregate
        => async (aggregate, cmd, ct) => {
            await act(aggregate, (TCommand)cmd, ct).NoContext();
            return aggregate;
        };

    public static HandleUntypedCommand<TAggregate> AsAct<TAggregate, TCommand>(this ActOnAggregate<TAggregate, TCommand> act) where TAggregate : Aggregate
        => (aggregate, cmd, _) => {
            act(aggregate, (TCommand)cmd);
            return ValueTask.FromResult(aggregate);
        };

    public static ResolveStoreFromCommand AsResolveStore<TCommand>(this ResolveStore<TCommand> resolveStore) where TCommand : class => cmd => resolveStore((TCommand)cmd);
}
