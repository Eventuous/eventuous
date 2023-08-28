// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class CommandServiceDelegates {
    public delegate Task ActOnAggregateAsync<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command, CancellationToken cancellationToken)
        where TAggregate : Aggregate;

    public delegate void ActOnAggregate<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command) where TAggregate : Aggregate;

    internal delegate ValueTask<T> HandleUntypedCommand<T>(T aggregate, object command, CancellationToken cancellationToken) where T : Aggregate;

    public delegate Task<TId> GetIdFromCommandAsync<TId, in TCommand>(TCommand command, CancellationToken cancellationToken)
        where TId : Id where TCommand : class;

    public delegate TId GetIdFromCommand<out TId, in TCommand>(TCommand command) where TId : Id where TCommand : class;

    internal delegate ValueTask<TId> GetIdFromUntypedCommand<TId>(object command, CancellationToken cancellationToken) where TId : Id;

    public delegate IAggregateStore ResolveStore<in TCommand>(TCommand command) where TCommand : class;

    internal delegate IAggregateStore ResolveStoreFromCommand(object command);
}
