using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Eventuous.Connectors.Base;

public static class Configuration {
    public static IConfigurationBuilder AddConfiguration(this WebApplicationBuilder builder)
        => builder.Configuration.AddYamlFile("config.yaml", false, true).AddEnvironmentVariables();
}
