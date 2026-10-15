// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics;

using Metrics;
using Tracing;

static class CommandServiceActivity {
    // Shared by every traced service, whatever its state type: the metrics listener only observes the latest
    // listener announced under a name, so a listener per service would hide the metrics of all but one.
    static readonly DiagnosticSource MetricsSource = new DiagnosticListener(CommandServiceMetrics.ListenerName);

    public static async Task<Result<T>> TryExecute<T, TCommand>(
            string                     appServiceTypeName,
            TCommand                   command,
            HandleCommand<T, TCommand> handleCommand,
            CancellationToken          cancellationToken
        ) where TCommand : class where T : State<T>, new() {
        var cmdName = command.GetType().Name;

        using var activity = StartActivity(appServiceTypeName, cmdName);
        // Nobody listening means nothing to record, so skip the measure and its context rather than allocate both.
        using var measure = MetricsSource.IsEnabled(Measure.EventName)
            ? Measure.Start(MetricsSource, new CommandServiceMetricsContext(appServiceTypeName, cmdName))
            : null;

        try {
            var result = await handleCommand(command, cancellationToken).NoContext();
            activity?.SetActivityStatus(result is { Success: true } ? ActivityStatus.Ok() : ActivityStatus.Error(result.Exception));
            if (!result.Success) measure?.SetError();

            return result;
        } catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            measure?.SetError();

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
