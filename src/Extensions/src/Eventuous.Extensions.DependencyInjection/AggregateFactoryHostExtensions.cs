// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.Hosting;

using DependencyInjection;

[PublicAPI]
public static class AggregateFactoryBuilderExtensions {
    /// <summary>
    /// Adds registered aggregate factories to the registry. The registry is then used by
    /// <see cref="CommandService{T,TState,TId}"/> and <see cref="AggregateStore"/>
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public static IHost UseAggregateFactory(this IHost host) {
        UseAggregateFactory(host.Services);
        return host;
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
