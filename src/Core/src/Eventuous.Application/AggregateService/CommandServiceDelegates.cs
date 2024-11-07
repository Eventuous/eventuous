// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class CommandServiceDelegates {
    internal delegate ValueTask<TAggregate> HandleUntypedCommand<TAggregate, TState>(TAggregate aggregate, object command, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new();

    internal delegate ValueTask<TId> GetIdFromUntypedCommand<TId>(object command, CancellationToken cancellationToken) where TId : Id;
}
