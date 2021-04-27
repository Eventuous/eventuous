using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Subscriptions {
    [PublicAPI]
    public static class ServiceCollectionExtensions {
        /// <summary>
        /// Register subscription as a hosted service
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/> container</param>
        /// <typeparam name="T">Subscription service type</typeparam>
        public static IServiceCollection AddSubscription<T>(this IServiceCollection services) where T : SubscriptionService {
            services.AddSingleton<T>();
            services.AddSingleton<IHostedService>(ctx => ctx.GetRequiredService<T>());
            return services;
        }

        /// <summary>
        /// Register subscription as a hosted service, with health checks
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/> container</param>
        /// <param name="checkName">Health check name</param>
        /// <param name="tags">Health check tags</param>
        /// <typeparam name="T">Subscription service type</typeparam>
        public static IServiceCollection AddSubscription<T>(this IServiceCollection services, string checkName, string[] tags)
            where T : SubscriptionService {
            services.AddSubscription<T>();
            services.AddHealthChecks().AddCheck<T>(checkName, null, tags);
            return services;
        }

        /// <summary>
        /// Registers event handler
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/> container</param>
        /// <typeparam name="T">Event handler type</typeparam>
        public static IServiceCollection AddEventHandler<T>(this IServiceCollection services) where T : class, IEventHandler
            => services.AddSingleton<IEventHandler, T>();
    }
}