using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Producers;

[PublicAPI]
public static class RegistrationExtensions {
    public static void AddEventProducer<T>(this IServiceCollection services, T producer)
        where T : class, IEventProducer {
        services.AddSingleton(producer);
        services.AddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());
        if (producer is IHostedService service) {
            services.AddSingleton(service);
        }
    }

    public static void AddEventProducer<T>(this IServiceCollection services, Func<IServiceProvider, T> getProducer)
        where T : class, IEventProducer {
        services.AddSingleton(getProducer);
        services.AddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());
        if (typeof(T).GetInterfaces().Contains(typeof(IHostedService))) {
            services.AddSingleton(sp => (sp.GetRequiredService<T>() as IHostedService)!);
        }
    }
}