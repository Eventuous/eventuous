// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.Hosting;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using Extensions;
using Diagnostics.HealthChecks;
using Logging;
using System.Diagnostics.CodeAnalysis;

[PublicAPI]
public static class SubscriptionRegistrationExtensions {
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
        TryAddSubscriptionHealthCheck(builder.Services);

        return builder.AddCheck<SubscriptionHealthCheck>(checkName, failureStatus, tags);
    }

    extension(IServiceCollection services) {
        public IServiceCollection AddCheckpointStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : class, ICheckpointStore {
            services.AddSingleton<T>();

            return AddCheckpointStoreInternal<T>(services);
        }

        public IServiceCollection AddCheckpointStore<T>(Func<IServiceProvider, T> getStore)
            where T : class, ICheckpointStore {
            services.AddSingleton(getStore);

            return AddCheckpointStoreInternal<T>(services);
        }

        public IServiceCollection AddSubscription<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(
                string                                   subscriptionId,
                Action<SubscriptionBuilder<T, TOptions>> configureSubscription
            ) where T : EventSubscription<TOptions> where TOptions : SubscriptionOptions {
            Ensure.NotNull(configureSubscription);
            var builder = new SubscriptionBuilder<T, TOptions>(Ensure.NotNull(services), Ensure.NotEmptyString(subscriptionId));
            configureSubscription(builder);
            TryAddSubscriptionHealthCheck(services);

            if (typeof(IMeasuredSubscription).IsAssignableFrom(typeof(T))) services.AddSingleton(GetEndOfStream);

            return services
                .AddSubscriptionBuilder(builder)
                .AddSingleton(sp => GetBuilder(sp).ResolveSubscription(sp))
                .AddSingleton<IHostedService, SubscriptionHostedService>(
                    sp => new(GetBuilder(sp).ResolveSubscription(sp), sp.GetService<ISubscriptionHealth>(), sp.GetService<ILoggerFactory>())
                );

            SubscriptionBuilder<T, TOptions> GetBuilder(IServiceProvider sp) => sp.GetSubscriptionBuilder<T, TOptions>(subscriptionId);

            GetSubscriptionEndOfStream GetEndOfStream(IServiceProvider sp) {
                var subscription = GetBuilder(sp).ResolveSubscription(sp) as IMeasuredSubscription;

                return subscription!.GetMeasure();
            }
        }
    }

    static void TryAddSubscriptionHealthCheck(IServiceCollection services) {
        services.TryAddSingleton<SubscriptionHealthCheck>();
        services.TryAddSingleton<ISubscriptionHealth>(sp => sp.GetRequiredService<SubscriptionHealthCheck>());
    }

    static IServiceCollection AddCheckpointStoreInternal<T>(IServiceCollection services) where T : class, ICheckpointStore {
        return EventuousDiagnostics.Enabled
            ? services.AddSingleton<ICheckpointStore>(sp => new MeasuredCheckpointStore(sp.GetRequiredService<T>()))
            : services.AddSingleton<ICheckpointStore>(sp => sp.GetRequiredService<T>());
    }
}
