using Eventuous.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Diagnostics.Registrations;

[PublicAPI]
public static class ApplicationServiceRegistration {
    public static IServiceCollection AddApplicationService<T, TState, TId>(
        this IServiceCollection services
    )
        where T : class, IApplicationService<TState, TId>
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId
        => services
            .AddSingleton<T>()
            .AddSingleton(
                sp => TracedApplicationService<TState, TId>.Trace(sp.GetRequiredService<T>())
            );

    public static IServiceCollection AddEventStore<T>(this IServiceCollection services)
        where T : class, IEventStore
        => services
            .AddSingleton<T>()
            .AddSingleton<IEventStore>(sp => sp.GetRequiredService<T>());
}