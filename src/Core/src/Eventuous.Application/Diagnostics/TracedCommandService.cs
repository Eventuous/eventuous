// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

public class TracedCommandService<TState> : ICommandService<TState> where TState : State<TState>, new() {
    public static ICommandService<TState> Trace(ICommandService<TState> appService) => new TracedCommandService<TState>(appService);

    ICommandService<TState> InnerService { get; }

    readonly string _appServiceTypeName;

    TracedCommandService(ICommandService<TState> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;
    }

    public Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class
        => CommandServiceActivity.TryExecute(
            _appServiceTypeName,
            command,
            InnerService.Handle,
            cancellationToken
        );
}

delegate Task<Result<T>> HandleCommand<T, in TCommand>(TCommand command, CancellationToken cancellationToken)
    where TCommand : class
    where T : State<T>, new();
