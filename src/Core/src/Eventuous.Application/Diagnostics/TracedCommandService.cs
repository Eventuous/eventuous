// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics;

public class TracedCommandService<T> : ICommandService<T> where T : Aggregate {
    public static ICommandService<T> Trace(ICommandService<T> appService)
        => new TracedCommandService<T>(appService);

    ICommandService<T> InnerService { get; }

    readonly string           _appServiceTypeName;
    readonly GetError<Result> _getError;
    readonly DiagnosticSource _metricsSource = new DiagnosticListener(CommandServiceMetrics.ListenerName);

    TracedCommandService(ICommandService<T> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;

        bool GetError(Result result, out Exception? exception) {
            if (result is ErrorResult err) {
                exception = err.Exception;
                return true;
            }

            exception = null;
            return false;
        }

        _getError = GetError;
    }

    public Task<Result> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class
        => CommandServiceActivity.TryExecute(
            _appServiceTypeName,
            command,
            _metricsSource,
            InnerService.Handle,
            _getError,
            cancellationToken
        );
}

public class TracedCommandService<T, TState, TId> : ICommandService<T, TState, TId>
    where TState : State<TState>, new()
    where TId : Id
    where T : Aggregate<TState> {
    public static ICommandService<T, TState, TId> Trace(ICommandService<T, TState, TId> appService)
        => new TracedCommandService<T, TState, TId>(appService);

    ICommandService<T, TState, TId> InnerService { get; }

    readonly DiagnosticSource         _metricsSource = new DiagnosticListener(CommandServiceMetrics.ListenerName);
    readonly string                   _appServiceTypeName;
    readonly GetError<Result<TState>> _getError;

    TracedCommandService(ICommandService<T, TState, TId> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;

        bool GetError(Result<TState> result, out Exception? exception) {
            if (result is ErrorResult<TState> err) {
                exception = err.Exception;
                return true;
            }

            exception = null;
            return false;
        }

        _getError = GetError;
    }

    public Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class
        => CommandServiceActivity.TryExecute(
            _appServiceTypeName,
            command,
            _metricsSource,
            InnerService.Handle,
            _getError,
            cancellationToken
        );
}

delegate Task<T> HandleCommand<T, in TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;

delegate bool GetError<in T>(T result, out Exception? exception);