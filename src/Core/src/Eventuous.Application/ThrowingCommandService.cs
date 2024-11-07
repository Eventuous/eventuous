// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Command service wrapper that throws an exception if the actual service returned an error result.
/// </summary>
/// <param name="inner">The actual command service.</param>
/// <typeparam name="TState"></typeparam>
public class ThrowingCommandService<TState>(ICommandService<TState> inner) : ICommandService<TState>
    where TState : State<TState>, new() {
    public async Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class {
        var result = await inner.Handle(command, cancellationToken);

        result.ThrowIfError();

        throw new ApplicationException($"Error handling command {command}");
    }
}
