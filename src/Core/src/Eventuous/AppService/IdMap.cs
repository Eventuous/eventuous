// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public delegate Task<TId> GetIdFromCommandAsync<TId, in TCommand>(TCommand command, CancellationToken cancellationToken)
    where TId : AggregateId where TCommand : class;

public delegate TId GetIdFromCommand<out TId, in TCommand>(TCommand command)
    where TId : AggregateId where TCommand : class;

class IdMap<TId> : Dictionary<Type, Func<object, CancellationToken, ValueTask<TId>>> where TId : AggregateId {
    public void AddCommand<TCommand>(GetIdFromCommand<TId, TCommand> getId) where TCommand : class
        => TryAdd(
            typeof(TCommand),
            (cmd, _) => new ValueTask<TId>(getId((TCommand)cmd))
        );

    public void AddCommand<TCommand>(GetIdFromCommandAsync<TId, TCommand> getId) where TCommand : class
        => TryAdd(
            typeof(TCommand),
            async (cmd, ct) => await getId((TCommand)cmd, ct)
        );
}
