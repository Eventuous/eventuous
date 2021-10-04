using Eventuous;
using Eventuous.EventStore.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class RegistrationExtensions {
    public static IServiceCollection AddStreamSubscription<T>(this IServiceCollection services, T? options = null)
        where T : StreamSubscriptionOptions, new() {
        services.AddSubscription(ConfigureSubscription);
        return services;

        StreamSubscription ConfigureSubscription(IServiceProvider provider) {
            var client = provider.GetService<EventStoreClient>() ?? CreateClient();

            var value = options ?? provider.GetService<IOptions<T>>()?.Value ?? new T();

            return new StreamSubscription(
                client,
                value with {
                    EventSerializer = value.EventSerializer ?? provider.GetService<IEventSerializer>(),
                    MetadataSerializer = value.MetadataSerializer ?? provider.GetService<IMetadataSerializer>()
                },
                provider.GetRequiredService<ICheckpointStore>(),
                provider.GetServices<IEventHandler>(),
                provider.GetService<ILoggerFactory>(),
                provider.GetService<SubscriptionGapMeasure>()
            );

            EventStoreClient CreateClient() {
                var settings = provider.GetService<EventStoreClientSettings>();

                return settings == null
                    ? throw new InvalidOperationException(
                        "Unable to resolve both EventStoreClient and EventStoreClientSettings"
                    )
                    : new EventStoreClient(settings);
            }
        }
    }
}