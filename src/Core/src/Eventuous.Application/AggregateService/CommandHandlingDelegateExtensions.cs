// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.CommandServiceDelegates;

namespace Eventuous;

static class CommandHandlingDelegateExtensions {
    public static GetIdFromUntypedCommand<TId> AsGetId<TId, TCommand>(this GetIdFromCommandAsync<TId, TCommand> getId) where TId : Id where TCommand : class
        => async (cmd, ct) => await getId((TCommand)cmd, ct);

    public static GetIdFromUntypedCommand<TId> AsGetId<TId, TCommand>(this GetIdFromCommand<TId, TCommand> getId) where TId : Id where TCommand : class
        => (cmd, _) => ValueTask.FromResult(getId((TCommand)cmd));

    public static HandleUntypedCommand<TAggregate, TState> AsAct<TAggregate, TState, TCommand>(this ActOnAggregateAsync<TAggregate, TState, TCommand> act)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => async (aggregate, cmd, ct) => {
            await act(aggregate, (TCommand)cmd, ct).NoContext();

            return aggregate;
        };

    public static HandleUntypedCommand<TAggregate, TState> AsAct<TAggregate, TState, TCommand>(this ActOnAggregate<TAggregate, TState, TCommand> act)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new()
        => (aggregate, cmd, _) => {
            act(aggregate, (TCommand)cmd);

            return ValueTask.FromResult(aggregate);
        };

    public static ResolveStoreFromCommand AsResolveStore<TCommand>(this ResolveStore<TCommand> resolveStore) where TCommand : class
        => cmd => resolveStore((TCommand)cmd);
}
