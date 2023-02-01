// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.Tools;

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
    where TId : AggregateId
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

delegate Task<T> HandleCommand<T, TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;

delegate bool GetError<in T>(T result, out Exception? exception);

static class CommandServiceActivity {
    public static async Task<T> TryExecute<T, TCommand>(
        string                     appServiceTypeName,
        TCommand                   command,
        DiagnosticSource           diagnosticSource,
        HandleCommand<T, TCommand> handleCommand,
        GetError<T>                getError,
        CancellationToken          cancellationToken
    ) where TCommand : class {
        var cmdName = command.GetType().Name;

        using var activity = StartActivity(appServiceTypeName, cmdName);

        using var measure = Measure.Start(
            diagnosticSource,
            new CommandServiceMetricsContext(appServiceTypeName, cmdName)
        );

        try {
            var result = await handleCommand(command, cancellationToken).NoContext();

            activity?.SetActivityStatus(
                getError(result, out var exception)
                    ? ActivityStatus.Error(exception)
                    : ActivityStatus.Ok()
            );

            return result;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            measure.SetError();
            throw;
        }
    }

    static Activity? StartActivity(string serviceName, string cmdName) {
        if (!EventuousDiagnostics.Enabled) return null;

        var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
                $"{Constants.Components.CommandService}.{serviceName}/{cmdName}",
                ActivityKind.Internal,
                parentContext: default,
                idFormat: ActivityIdFormat.W3C,
                tags: EventuousDiagnostics.Tags
            )
            ?.SetTag(TelemetryTags.Eventuous.Command, cmdName)
            .Start();

        return activity;
    }
}
