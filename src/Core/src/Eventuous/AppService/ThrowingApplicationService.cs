// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class ThrowingApplicationService<T, TState, TId> : IApplicationService<T, TState, TId>, IApplicationService<T>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : AggregateId {
    readonly IApplicationService<T, TState, TId> _inner;

    public ThrowingApplicationService(IApplicationService<T, TState, TId> inner) => _inner = inner;

    public async Task<Result<TState>> Handle(object command, CancellationToken cancellationToken) {
        var result = await _inner.Handle(command, cancellationToken);

        if (result is ErrorResult<TState> error)
            throw error.Exception ?? new ApplicationException($"Error handling command {command}");

        return result;
    }

    async Task<Result> IApplicationService.Handle(object command, CancellationToken cancellationToken) {
        var result = await Handle(command, cancellationToken).NoContext();

        return result switch {
            OkResult<TState>(var aggregateState, var enumerable, _) => new OkResult(aggregateState, enumerable),
            ErrorResult<TState> error => throw error.Exception
                                            ?? new ApplicationException($"Error handling command {command}"),
            _ => throw new ApplicationException("Unknown result type")
        };
    }
}
