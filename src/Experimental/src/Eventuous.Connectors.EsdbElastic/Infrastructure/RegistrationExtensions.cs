using Elasticsearch.Net;
using Nest;
using static System.String;

namespace Eventuous.Connectors.EsdbElastic.Infrastructure;

public static class RegistrationExtensions {
    public static IServiceCollection AddElasticClient(
        this IServiceCollection                       services,
        string                                        connectionString,
        string?                                       apiKey,
        Func<ConnectionSettings, ConnectionSettings>? configureSettings = null
    )
        => services.AddSingleton<IElasticClient>(CreateElasticClient(connectionString, apiKey, configureSettings));

    public static ElasticClient CreateElasticClient(
        string                                        connectionString,
        string?                                       apiKey,
        Func<ConnectionSettings, ConnectionSettings>? configureSettings = null
    ) {
        var settings = new ConnectionSettings(new Uri(connectionString));

        if (configureSettings is not null) settings = configureSettings(settings);

        return IsNullOrEmpty(apiKey)
            ? new ElasticClient(settings)
            : new ElasticClient(settings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKey)));
    }
}
