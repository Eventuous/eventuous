// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics;

using Metrics;
using Tracing;

static class CommandServiceActivity {
    public static async Task<Result<T>> TryExecute<T, TCommand>(
            string                     appServiceTypeName,
            TCommand                   command,
            DiagnosticSource           diagnosticSource,
            HandleCommand<T, TCommand> handleCommand,
            CancellationToken          cancellationToken
        ) where TCommand : class where T : State<T>, new() {
        var cmdName = command.GetType().Name;

        using var activity = StartActivity(appServiceTypeName, cmdName);
        using var measure  = Measure.Start(diagnosticSource, new CommandServiceMetricsContext(appServiceTypeName, cmdName));

        try {
            var result = await handleCommand(command, cancellationToken).NoContext();
            activity?.SetActivityStatus(result is { Success: true } ? ActivityStatus.Ok() : ActivityStatus.Error(result.Exception));
            if (!result.Success) measure.SetError();

            return result;
        } catch (Exception e) {
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
