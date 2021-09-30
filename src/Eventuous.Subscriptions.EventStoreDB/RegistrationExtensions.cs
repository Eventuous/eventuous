using Eventuous;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.EventStoreDB;
using Microsoft.Extensions.Logging;
using StreamSubscription = Eventuous.Subscriptions.EventStoreDB.StreamSubscription;

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.DependencyInjection; 

[PublicAPI]
public static class RegistrationExtensions {
    public static IServiceCollection AddStreamSubscription(this IServiceCollection services, StreamSubscriptionOptions options) {
        services.AddSubscription(ConfigureSubscription);
        return services;

        StreamSubscription ConfigureSubscription(IServiceProvider provider) {
            var client = provider.GetService<EventStoreClient>() ?? CreateClient();

            return new StreamSubscription(
                client,
                options,
                provider.GetRequiredService<ICheckpointStore>(),
                provider.GetServices<IEventHandler>(),
                provider.GetService<IEventSerializer>(),
                provider.GetService<IMetadataSerializer>(),
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