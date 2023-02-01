// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

public static class ObsoleteExtensions {
    [Obsolete("Use AddCommandService instead")]
    public static IServiceCollection AddApplicationService<T, TAggregate>(this IServiceCollection services)
        where T : class, ICommandService<TAggregate>
        where TAggregate : Aggregate
        => services.AddCommandService<T, TAggregate>();

    [Obsolete("Use AddCommandService instead")]
    public static IServiceCollection AddApplicationService<T, TAggregate, TState, TId>(
        this IServiceCollection services,
        bool                    throwOnError = false
    )
        where T : class, ICommandService<TAggregate, TState, TId>
        where TState : State<TState>, new()
        where TId : AggregateId
        where TAggregate : Aggregate<TState>
        => services.AddCommandService<T, TAggregate, TState, TId>(throwOnError);

    [Obsolete("Use AddCommandService instead")]
    public static IServiceCollection AddApplicationService<T, TAggregate>(
        this IServiceCollection   services,
        Func<IServiceProvider, T> getService
    )
        where T : class, IApplicationService<TAggregate>
        where TAggregate : Aggregate
        => services.AddCommandService<T, TAggregate>(getService);
}
