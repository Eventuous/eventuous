using System.Diagnostics;

namespace Eventuous.Diagnostics.Tracing;

public class TracedApplicationService<T> : IApplicationService<T> where T : Aggregate {
    public static IApplicationService<T> Trace(IApplicationService<T> appService)
        => new TracedApplicationService<T>(appService);

    IApplicationService<T> Inner { get; }

    TracedApplicationService(IApplicationService<T> appService) => Inner = appService;

    public async Task<Result> Handle<TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    ) where TCommand : class {
        using var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            "hande-command",
            ActivityKind.Internal,
            parentContext: default,
            idFormat: ActivityIdFormat.W3C
        )?.Start();
        
        return await Inner.Handle(command, cancellationToken).NoContext();
    }
}

public class TracedApplicationService<TState, TId> : IApplicationService<TState, TId>
    where TState : AggregateState<TState, TId>, new() where TId : AggregateId {
    public static IApplicationService<TState, TId> Trace(IApplicationService<TState, TId> appService)
        => new TracedApplicationService<TState, TId>(appService);

    IApplicationService<TState, TId> Inner { get; }

    TracedApplicationService(IApplicationService<TState, TId> appService)
        => Inner = appService;

    public async Task<Result<TState, TId>> Handle<TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    ) where TCommand : class {
        using var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            "hande-command",
            ActivityKind.Internal,
            parentContext: default,
            idFormat: ActivityIdFormat.W3C
        )?.Start();
        
        return await Inner.Handle(command, cancellationToken).NoContext();
    }
}