// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics;

public class TracedCommandService<TAggregate>(ICommandService<TAggregate> appService) : ICommandService<TAggregate> where TAggregate : Aggregate {
    public static ICommandService<TAggregate> Trace(ICommandService<TAggregate> appService)
        => new TracedCommandService<TAggregate>(appService);

    ICommandService<TAggregate> InnerService { get; } = appService;

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

    public Task<Result> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class
        => CommandServiceActivity.TryExecute(
            _appServiceTypeName,
            command,
            _metricsSource,
            InnerService.Handle,
            GetError,
            cancellationToken
        );
}

public class TracedCommandService<TAggregate, TState, TId>(ICommandService<TAggregate, TState, TId> appService) : ICommandService<TAggregate, TState, TId>
    where TState : State<TState>, new()
    where TId : Id
    where TAggregate : Aggregate<TState> {
    public static ICommandService<TAggregate, TState, TId> Trace(ICommandService<TAggregate, TState, TId> appService)
        => new TracedCommandService<TAggregate, TState, TId>(appService);

    ICommandService<TAggregate, TState, TId> InnerService { get; } = appService;

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

    public Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
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

delegate Task<T> HandleCommand<T, in TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;

delegate bool GetError<in T>(T result, out Exception? exception);
