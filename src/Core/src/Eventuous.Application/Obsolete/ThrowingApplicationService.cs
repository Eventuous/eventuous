// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[Obsolete("Use ThrowingCommandService instead")]
public class ThrowingApplicationService<T, TState, TId>
    : ThrowingCommandService<T, TState, TId>,
        IApplicationService<T, TState, TId>, IApplicationService<T>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : AggregateId {
    public ThrowingApplicationService(ICommandService<T, TState, TId> inner) : base(inner) { }
}