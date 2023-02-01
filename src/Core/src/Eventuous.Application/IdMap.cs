// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;

namespace Eventuous;

public delegate Task<TId> GetIdFromCommandAsync<TId, in TCommand>(TCommand command, CancellationToken cancellationToken)
    where TId : AggregateId where TCommand : class;

public delegate TId GetIdFromCommand<out TId, in TCommand>(TCommand command) where TId : AggregateId where TCommand : class;

class IdMap<TId> where TId : AggregateId {
    readonly TypeMap<Func<object, CancellationToken, ValueTask<TId>>> _typeMap = new();

    public void AddCommand<TCommand>(GetIdFromCommand<TId, TCommand> getId) where TCommand : class
        => _typeMap.Add<TCommand>((cmd, _) => new ValueTask<TId>(getId((TCommand)cmd)));

    public void AddCommand<TCommand>(GetIdFromCommandAsync<TId, TCommand> getId) where TCommand : class
        => _typeMap.Add<TCommand>(async (cmd, ct) => await getId((TCommand)cmd, ct));

    public bool TryGet<TCommand>([NotNullWhen(true)] out Func<object, CancellationToken, ValueTask<TId>>? getId) where TCommand : class
        => _typeMap.TryGetValue<TCommand>(out getId);
}
