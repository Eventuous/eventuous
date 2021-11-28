using System.Reflection;

namespace Eventuous.EventStore.Subscriptions; 

static class EventStoreExtensions {
    public static EventStoreClientSettings GetSettings(this EventStoreClient client) {
        var prop =
            typeof(EventStoreClient).GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance);

        var getter = prop!.GetGetMethod(true);
        return (EventStoreClientSettings) getter!.Invoke(client, null)!;
    }

    public static EventStoreClientSettings Copy(this EventStoreClientSettings settings)
        => new() {
            Interceptors         = settings.Interceptors,
            ChannelCredentials   = settings.ChannelCredentials,
            ConnectionName       = settings.ConnectionName,
            ConnectivitySettings = settings.ConnectivitySettings,
            DefaultCredentials   = settings.DefaultCredentials,
            LoggerFactory        = settings.LoggerFactory,
            OperationOptions     = settings.OperationOptions,
        };
}