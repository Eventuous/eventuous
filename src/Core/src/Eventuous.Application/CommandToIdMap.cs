// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public delegate Task<TId> GetIdFromCommandAsync<TId, in TCommand>(TCommand command, CancellationToken cancellationToken)
    where TId : AggregateId where TCommand : class;

public delegate TId GetIdFromCommand<out TId, in TCommand>(TCommand command) where TId : AggregateId where TCommand : class;

delegate ValueTask<TId> GetIdFromUntypedCommand<TId>(object command, CancellationToken cancellationToken)
    where TId : AggregateId;

class IdMap<TId> where TId : AggregateId {
    readonly TypeMap<GetIdFromUntypedCommand<TId>> _typeMap = new();

    public void AddCommand<TCommand>(GetIdFromCommand<TId, TCommand> getId) where TCommand : class
        => _typeMap.Add<TCommand>((cmd, _) => new ValueTask<TId>(getId((TCommand)cmd)));

    public void AddCommand<TCommand>(GetIdFromCommandAsync<TId, TCommand> getId) where TCommand : class
        => _typeMap.Add<TCommand>(async (cmd, ct) => await getId((TCommand)cmd, ct));

    internal bool TryGet<TCommand>([NotNullWhen(true)] out GetIdFromUntypedCommand<TId>? getId) where TCommand : class
        => _typeMap.TryGetValue<TCommand>(out getId);
}
