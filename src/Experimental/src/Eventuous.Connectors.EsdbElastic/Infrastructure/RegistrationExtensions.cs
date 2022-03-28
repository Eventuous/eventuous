using Elasticsearch.Net;
using Nest;
using static System.String;

namespace Eventuous.Connectors.EsdbElastic.Infrastructure;

static class RegistrationExtensions {
    public static IServiceCollection AddElasticClient(
        this IServiceCollection                       services,
        string                                        connectionString,
        string?                                       apiKey,
        Func<ConnectionSettings, ConnectionSettings>? configureSettings = null
    )
        => services.AddSingleton<IElasticClient>(CreateElasticClient(connectionString, apiKey, configureSettings));

    static ElasticClient CreateElasticClient(
        string                                        connectionString,
        string?                                       apiKey,
        Func<ConnectionSettings, ConnectionSettings>? configureSettings = null
    ) {
        var pool     = new SingleNodeConnectionPool(new Uri(connectionString));
        var settings = new ConnectionSettings(pool, (def, _) => new ElasticSerializer(def));

        if (configureSettings is not null) settings = configureSettings(settings);

        return IsNullOrEmpty(apiKey)
            ? new ElasticClient(settings)
            : new ElasticClient(settings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKey)));
    }
}
