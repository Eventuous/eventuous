// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class SubscriptionRegistrationExtensions {
    public static IServiceCollection AddSubscription<T, TOptions>(
        this IServiceCollection                  services,
        string                                   subscriptionId,
        Action<SubscriptionBuilder<T, TOptions>> configureSubscription
    ) where T : EventSubscription<TOptions> where TOptions : SubscriptionOptions {
        Ensure.NotNull(configureSubscription);

        var builder = new SubscriptionBuilder<T, TOptions>(
            Ensure.NotNull(services),
            Ensure.NotEmptyString(subscriptionId)
        );

        configureSubscription(builder);

        services.TryAddSingleton<ISubscriptionHealth, SubscriptionHealthCheck>();

        if (typeof(IMeasuredSubscription).IsAssignableFrom(typeof(T))) services.AddSingleton(GetGapMeasure);

        return services
            .AddSubscriptionBuilder(builder)
            .AddSingleton(sp => GetBuilder(sp).ResolveSubscription(sp))
            .AddSingleton<IHostedService>(
                sp =>
                    new SubscriptionHostedService(
                        GetBuilder(sp).ResolveSubscription(sp),
                        sp.GetService<ISubscriptionHealth>(),
                        sp.GetService<ILoggerFactory>()
                    )
            );

        SubscriptionBuilder<T, TOptions> GetBuilder(IServiceProvider sp)
            => sp.GetSubscriptionBuilder<T, TOptions>(subscriptionId);

        GetSubscriptionGap GetGapMeasure(IServiceProvider sp) {
            var subscription = GetBuilder(sp).ResolveSubscription(sp) as IMeasuredSubscription;
            return subscription!.GetMeasure();
        }
    }

    /// <summary>
    /// Adds a health check for subscriptions. All subscriptions will be monitored by one check.
    /// </summary>
    /// <param name="builder">Health checks builder</param>
    /// <param name="checkName">Name of the health check</param>
    /// <param name="failureStatus">Health status for unhealthy subscriptions</param>
    /// <param name="tags">Health check tags list</param>
    /// <returns></returns>
    public static IHealthChecksBuilder AddSubscriptionsHealthCheck(
        this IHealthChecksBuilder builder,
        string                    checkName,
        HealthStatus?             failureStatus,
        string[]                  tags
    ) {
        builder.Services.TryAddSingleton<SubscriptionHealthCheck>();

        builder.Services.TryAddSingleton<ISubscriptionHealth>(
            sp => sp.GetRequiredService<SubscriptionHealthCheck>()
        );

        return builder.AddCheck<SubscriptionHealthCheck>(checkName, failureStatus, tags);
    }

    public static IServiceCollection AddCheckpointStore<T>(this IServiceCollection services)
        where T : class, ICheckpointStore {
        services.AddSingleton<T>();

        return EventuousDiagnostics.Enabled
            ? services.AddSingleton<ICheckpointStore>(
                sp => new MeasuredCheckpointStore(sp.GetRequiredService<T>())
            )
            : services.AddSingleton<ICheckpointStore>(sp => sp.GetRequiredService<T>());
    }

    public static IServiceCollection AddCheckpointStore<T>(
        this IServiceCollection   services,
        Func<IServiceProvider, T> getStore
    ) where T : class, ICheckpointStore {
        services.AddSingleton(getStore);

        return EventuousDiagnostics.Enabled
            ? services.AddSingleton<ICheckpointStore>(
                sp => new MeasuredCheckpointStore(sp.GetRequiredService<T>())
            )
            : services.AddSingleton<ICheckpointStore>(sp => sp.GetRequiredService<T>());
    }
}
