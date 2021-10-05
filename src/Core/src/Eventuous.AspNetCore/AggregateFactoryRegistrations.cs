using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.AspNetCore;

[PublicAPI]
public static class AggregateFactoryRegistrations {
    public static IServiceCollection AddAggregateFactory<T, TState, TId>(
        this IServiceCollection   services,
        Func<IServiceProvider, T> createInstance
    )
        where T : Aggregate<TState, TId>
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId
        => services.AddSingleton(new ResolveAggregateFactory(typeof(T), createInstance));

    public static IApplicationBuilder UseAggregateFactory(this IApplicationBuilder builder) {
        var resolvers = builder.ApplicationServices.GetServices<ResolveAggregateFactory>();

        foreach (var resolver in resolvers) {
            AggregateFactoryRegistry.Instance.UnsafeCreateAggregateUsing(
                resolver.Type,
                () => resolver.CreateInstance(builder.ApplicationServices)
            );
        }

        return builder;
    }

    record ResolveAggregateFactory(Type Type, Func<IServiceProvider, Aggregate> CreateInstance);
}
