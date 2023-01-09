// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.Tools;

namespace Eventuous.Diagnostics;

public class TracedApplicationService<T> : IApplicationService<T> where T : Aggregate {
    public static IApplicationService<T> Trace(IApplicationService<T> appService)
        => new TracedApplicationService<T>(appService);

    IApplicationService<T> InnerService { get; }

    readonly string                _appServiceTypeName;
    readonly HandleCommand<Result> _handleCommand;
    readonly GetError<Result>      _getError;

    readonly DiagnosticSource _metricsSource = new DiagnosticListener(ApplicationServiceMetrics.ListenerName);

    TracedApplicationService(IApplicationService<T> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;
        _handleCommand      = (cmd, ct) => InnerService.Handle(cmd, ct);

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
        => AppServiceActivity.TryExecute(_appServiceTypeName,
            command,
            _metricsSource,
            _handleCommand,
            _getError,
            cancellationToken
        );
}

public class TracedApplicationService<T, TState, TId> : IApplicationService<T, TState, TId>
    where TState : State<TState>, new()
    where TId : AggregateId
    where T : Aggregate<TState> {
    public static IApplicationService<T, TState, TId> Trace(IApplicationService<T, TState, TId> appService)
        => new TracedApplicationService<T, TState, TId>(appService);

    IApplicationService<T, TState, TId> InnerService { get; }

    readonly DiagnosticSource _metricsSource = new DiagnosticListener(ApplicationServiceMetrics.ListenerName);

    readonly string                        _appServiceTypeName;
    readonly HandleCommand<Result<TState>> _handleCommand;
    readonly GetError<Result<TState>>      _getError;

    TracedApplicationService(IApplicationService<T, TState, TId> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;
        _handleCommand      = (cmd, ct) => InnerService.Handle(cmd, ct);

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
        => AppServiceActivity.TryExecute(_appServiceTypeName,
            command,
            _metricsSource,
            _handleCommand,
            _getError,
            cancellationToken
        );
}

delegate Task<T> HandleCommand<T>(object command, CancellationToken cancellationToken);

delegate bool GetError<in T>(T result, out Exception? exception);

static class AppServiceActivity {
    public static async Task<T> TryExecute<T>(
        string            appServiceTypeName,
        object            command,
        DiagnosticSource  diagnosticSource,
        HandleCommand<T>  handleCommand,
        GetError<T>       getError,
        CancellationToken cancellationToken
    ) {
        var cmdName = command.GetType().Name;

        using var activity = StartActivity(appServiceTypeName, cmdName);
        using var measure = Measure.Start(diagnosticSource,
            new AppServiceMetricsContext(appServiceTypeName, cmdName)
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
                $"{Constants.Components.AppService}.{serviceName}/{cmdName}",
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