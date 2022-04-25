using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Eventuous.Connectors.Base;

public static class Configuration {
    public static IConfigurationBuilder AddConfiguration(this WebApplicationBuilder builder)
        => builder.Configuration.AddYamlFile("config.yaml", false, true).AddEnvironmentVariables();

    public static ConnectorConfig<TSource, TTarget>
        GetConnectorConfig<TSource, TTarget>(this IConfiguration configuration)
        where TSource : class where TTarget : class
        => configuration.Get<ConnectorConfig<TSource, TTarget>>();
}
