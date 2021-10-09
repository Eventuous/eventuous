using Eventuous;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Monitoring;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class SubscriptionRegistrationExtensions {
    internal static List<ISubscriptionBuilder> Builders { get; } = new();

    public static ISubscriptionBuilder AddSubscription<T, TOptions>(
        this IServiceCollection services,
        string                  subscriptionId,
        Action<TOptions>?       configureOptions = null
    )
        where T : SubscriptionService<TOptions>
        where TOptions : SubscriptionOptions {
        ISubscriptionBuilder<T, TOptions> builder = new DefaultSubscriptionBuilder<T, TOptions>(
            Ensure.NotNull(services, nameof(services)),
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId))
        );

        if (Builders.Any(x => x.SubscriptionId == subscriptionId)) {
            throw new InvalidOperationException($"Subscription with id {subscriptionId} has already been registered");
        }

        Builders.Add(builder);

        services.Configure<TOptions>(subscriptionId, ConfigureOptions);

        services.AddSingleton(sp => builder.Resolve(sp));
        services.AddSingleton<IHostedService>(sp => builder.Resolve(sp));

        services.TryAddSingleton<SubscriptionHealthCheck>();
        services.AddSingleton<IReportHealth>(sp => builder.Resolve(sp));

        return builder;

        void ConfigureOptions(TOptions options) {
            options.SubscriptionId = subscriptionId;
            configureOptions?.Invoke(options);
        }
    }

    public static ISubscriptionBuilder AddEventHandler<THandler>(this ISubscriptionBuilder builder)
        where THandler : class, IEventHandler {
        builder.Services.AddSingleton<THandler>();
        builder.Services.AddSingleton<ResolveHandler>(Resolve);
        return builder;

        IEventHandler? Resolve(IServiceProvider sp, string id)
            => id == builder.SubscriptionId ? sp.GetService<THandler>() : null;
    }

    public static ISubscriptionBuilder AddEventHandler<THandler>(
        this ISubscriptionBuilder        builder,
        Func<IServiceProvider, THandler> getHandler
    )
        where THandler : class, IEventHandler {
        builder.Services.AddSingleton(getHandler);
        builder.Services.AddSingleton<ResolveHandler>(Resolve);
        return builder;

        IEventHandler? Resolve(IServiceProvider sp, string id)
            => id == builder.SubscriptionId ? sp.GetService<THandler>() : null;
    }

    public static IHealthChecksBuilder AddSubscriptionsCheck(
        this IHealthChecksBuilder builder,
        string                    checkName,
        string[]                  tags
    ) => builder.AddCheck<SubscriptionHealthCheck>(checkName, null, tags);

    public static IServiceCollection AddCheckpointStore<T>(this IServiceCollection services)
        where T : class, ICheckpointStore {
        services.AddSingleton<ICheckpointStore, T>();
        return services;
    }
}