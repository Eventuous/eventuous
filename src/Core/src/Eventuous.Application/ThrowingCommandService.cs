// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.ExceptionServices;
namespace Eventuous;

public class ThrowingCommandService<T, TState, TId>(ICommandService<T, TState, TId> inner) : ICommandService<T, TState, TId>, ICommandService<T>
    where T : Aggregate<TState>
    where TState : State<TState>, new()
    where TId : Id {
    public async Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class {
        var result = await inner.Handle(command, cancellationToken);

        if (result is ErrorResult<TState> error) {
            if (error.Exception is not null) {
                ExceptionDispatchInfo.Capture(error.Exception).Throw();
            }
            throw new ApplicationException($"Error handling command {command}");
        }

        return result;
    }

    async Task<Result> ICommandService.Handle<TCommand>(TCommand command, CancellationToken cancellationToken) {
        var result = await Handle(command, cancellationToken).NoContext();

        return result switch {
            OkResult<TState>(var aggregateState, var enumerable, _) => new OkResult(aggregateState, enumerable),
            ErrorResult<TState> error => throw error.Exception
             ?? new ApplicationException($"Error handling command {command}"),
            _ => throw new ApplicationException("Unknown result type")
        };
    }
}
