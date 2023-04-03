using Microsoft.AspNetCore.Builder;

namespace Eventuous.AspNetCore.Web;

[PublicAPI]
public static class AggregateFactoryBuilderExtensions {
    /// <summary>
    /// Adds registered aggregate factories to the registry. The registry is then used by
    /// <see cref="CommandService{T,TState,TId}"/> and <see cref="AggregateStore"/>
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseAggregateFactory(this IApplicationBuilder builder) {
        UseAggregateFactory(builder.ApplicationServices);
        return builder;
    }

    static void UseAggregateFactory(IServiceProvider sp) {
        var resolvers = sp.GetServices<ResolveAggregateFactory>();
        var registry  = sp.GetService<AggregateFactoryRegistry>() ?? AggregateFactoryRegistry.Instance;

        foreach (var resolver in resolvers) {
            registry.UnsafeCreateAggregateUsing(
                resolver.Type,
                () => resolver.CreateInstance(sp)
            );
        }
    }
}