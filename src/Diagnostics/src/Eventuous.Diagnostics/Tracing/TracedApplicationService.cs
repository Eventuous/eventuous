namespace Eventuous.Diagnostics.Tracing;

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
        return await Inner.Handle(command, cancellationToken);
    }
}