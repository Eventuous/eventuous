// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedTypeParameter

namespace Eventuous;

public interface IApplicationService {
    Task<Result> Handle(object command, CancellationToken cancellationToken);
}

public interface IApplicationService<T> : IApplicationService where T : Aggregate { }

public interface IApplicationService<T, TState, TId>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : AggregateId {
    Task<Result<TState>> Handle(object command, CancellationToken cancellationToken);
}
