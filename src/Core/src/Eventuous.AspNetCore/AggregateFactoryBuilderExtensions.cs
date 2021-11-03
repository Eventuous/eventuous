using Microsoft.Extensions.DependencyInjection;

// ReSharper disable CheckNamespace
namespace Microsoft.AspNetCore.Builder;

[PublicAPI]
public static class AggregateFactoryBuilderExtensions {
    /// <summary>
    /// Adds registered aggregate factories to the registry. The registry is then used by
    /// <see cref="ApplicationService{T,TState,TId}"/> and <see cref="AggregateStore"/>
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseAggregateFactory(this IApplicationBuilder builder) {
        var resolvers = builder.ApplicationServices.GetServices<ResolveAggregateFactory>();

        var registry = builder.ApplicationServices.GetService<AggregateFactoryRegistry>()
                    ?? AggregateFactoryRegistry.Instance;

        foreach (var resolver in resolvers) {
            registry.UnsafeCreateAggregateUsing(
                resolver.Type,
                () => resolver.CreateInstance(builder.ApplicationServices)
            );
        }

        return builder;
    }
}