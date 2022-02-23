using System.Diagnostics;

namespace Eventuous.Diagnostics.Tracing;

public class TracedApplicationService<T> : IApplicationService<T> where T : Aggregate {
    public static IApplicationService<T> Trace(IApplicationService<T> appService)
        => new TracedApplicationService<T>(appService);

    IApplicationService<T> InnerService { get; }

    readonly string _appServiceTypeName;

    TracedApplicationService(IApplicationService<T> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;
    }

    public async Task<Result> Handle(object command, CancellationToken cancellationToken) {
        using var activity = AppServiceActivity.StartActivity(_appServiceTypeName, command);

        try {
            var result = await InnerService.Handle(command, cancellationToken).NoContext();

            if (activity != null) {
                if (result is ErrorResult error) {
                    activity.SetActivityStatus(ActivityStatus.Error(error.Exception));
                }
                else {
                    activity.SetActivityStatus(ActivityStatus.Ok());
                }
            }

            return result;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            throw;
        }
    }
}

public class TracedApplicationService<T, TState, TId> : IApplicationService<T, TState, TId>
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId
    where T : Aggregate<TState, TId> {
    public static IApplicationService<T, TState, TId> Trace(IApplicationService<T, TState, TId> appService)
        => new TracedApplicationService<T, TState, TId>(appService);

    IApplicationService<T, TState, TId> InnerService { get; }

    readonly string _appServiceTypeName;

    TracedApplicationService(IApplicationService<T, TState, TId> appService) {
        _appServiceTypeName = appService.GetType().Name;
        InnerService        = appService;
    }

    public async Task<Result<TState, TId>> Handle(object command, CancellationToken cancellationToken) {
        using var activity = AppServiceActivity.StartActivity(_appServiceTypeName, command);

        try {
            var result = await InnerService.Handle(command, cancellationToken).NoContext();

            if (activity != null) {
                if (result is ErrorResult<TState, TId> error) {
                    activity.SetActivityStatus(ActivityStatus.Error(error.Exception));
                }
                else {
                    activity.SetActivityStatus(ActivityStatus.Ok());
                }
            }

            return result;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            throw;
        }
    }
}

static class AppServiceActivity {
    public static Activity? StartActivity(string serviceName, object command) {
        if (!EventuousDiagnostics.Enabled) return null;

        var cmdName = command.GetType().Name;
        var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            $"{Constants.AppServicePrefix}.{serviceName}/{cmdName}",
            ActivityKind.Internal,
            parentContext: default,
            idFormat: ActivityIdFormat.W3C,
            tags: EventuousDiagnostics.Tags
        )?.Start();

        activity?.SetTag(TelemetryTags.Eventuous.Command, cmdName);
        return activity;
    }
}