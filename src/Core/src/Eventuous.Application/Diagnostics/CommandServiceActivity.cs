// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.Tools;

namespace Eventuous.Diagnostics;

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
