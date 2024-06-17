// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics;

public class TracedFunctionalService<TState> : ICommandService<TState> where TState : State<TState>, new() {
    public static ICommandService<TState> Trace(ICommandService<TState> appService)
        => new TracedFunctionalService<TState>(appService);

    ICommandService<TState> InnerService { get; }

    readonly string           _appServiceTypeName;
    readonly DiagnosticSource _metricsSource = new DiagnosticListener(CommandServiceMetrics.ListenerName);

    TracedFunctionalService(ICommandService<TState> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;
    }

    public Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class
        => CommandServiceActivity.TryExecute(
            _appServiceTypeName,
            command,
            _metricsSource,
            InnerService.Handle,
            cancellationToken
        );
}
