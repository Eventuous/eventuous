// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics;

public class TracedFunctionalService<T> : IFuncCommandService<T> where T : State<T>, new() {
    public static IFuncCommandService<T> Trace(IFuncCommandService<T> appService)
        => new TracedFunctionalService<T>(appService);

    IFuncCommandService<T> InnerService { get; }

    readonly string           _appServiceTypeName;
    readonly GetError<Result> _getError;
    readonly DiagnosticSource _metricsSource = new DiagnosticListener(CommandServiceMetrics.ListenerName);

    TracedFunctionalService(IFuncCommandService<T> appService) {
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
            (command1, cancellationToken1) => InnerService.Handle(command1, cancellationToken1),
            _getError,
            cancellationToken
        );
}
