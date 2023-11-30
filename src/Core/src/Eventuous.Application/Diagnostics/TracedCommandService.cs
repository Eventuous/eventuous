// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics;

public class TracedCommandService<T>(ICommandService<T> appService) : ICommandService<T> where T : Aggregate {
    public static ICommandService<T> Trace(ICommandService<T> appService)
        => new TracedCommandService<T>(appService);

    ICommandService<T> InnerService { get; } = appService;

    readonly string           _appServiceTypeName = appService.GetType().Name;
    readonly DiagnosticSource _metricsSource      = new DiagnosticListener(CommandServiceMetrics.ListenerName);

    static bool GetError(Result result, out Exception? exception) {
        if (result is ErrorResult err) {
            exception = err.Exception;

            return true;
        }

        exception = null;

        return false;
    }

    public Task<Result> Handle<TCommand>(TCommand command,AmendEvent  @event ,CancellationToken cancellationToken)
        where TCommand : class
        => CommandServiceActivity.TryExecute(
            _appServiceTypeName,
            command,
            _metricsSource,
            InnerService.Handle,
            GetError,
            cancellationToken
        );
}

public class TracedCommandService<T, TState, TId>(ICommandService<T, TState, TId> appService) : ICommandService<T, TState, TId>
    where TState : State<TState>, new()
    where TId : Id
    where T : Aggregate<TState> {
    public static ICommandService<T, TState, TId> Trace(ICommandService<T, TState, TId> appService)
        => new TracedCommandService<T, TState, TId>(appService);

    ICommandService<T, TState, TId> InnerService { get; } = appService;

    readonly DiagnosticSource _metricsSource      = new DiagnosticListener(CommandServiceMetrics.ListenerName);
    readonly string           _appServiceTypeName = appService.GetType().Name;

    static bool GetError(Result<TState> result, out Exception? exception) {
        if (result is ErrorResult<TState> err) {
            exception = err.Exception;

            return true;
        }

        exception = null;

        return false;
    }

    public Task<Result<TState>> Handle<TCommand>(TCommand command, AmendEvent amendEvent, CancellationToken cancellationToken)
        where TCommand : class
        => CommandServiceActivity.TryExecute(
            _appServiceTypeName,
            command,
            _metricsSource,
            InnerService.Handle,
            GetError,
            cancellationToken
        );
}

delegate Task<T> HandleCommand<T, in TCommand>(TCommand command, AmendEvent amendEvent, CancellationToken cancellationToken) where TCommand : class;

delegate bool GetError<in T>(T result, out Exception? exception);
