using System;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Producers {
    [PublicAPI]
    public static class RegistrationExtensions {
        public static void AddEventProducer<T>(this IServiceCollection services, T producer)
            where T : class, IEventProducer {
            services.AddSingleton(producer);
            services.AddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());
            services.AddHostedService<ProducersHostedService>();
        }

        public static void AddEventProducer<T>(this IServiceCollection services, Func<IServiceProvider, T> getProducer)
            where T : class, IEventProducer {
            services.AddSingleton(getProducer);
            services.AddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());
            services.AddHostedService<ProducersHostedService>();
        }
    }
}