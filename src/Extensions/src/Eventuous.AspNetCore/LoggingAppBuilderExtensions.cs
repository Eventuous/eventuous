using Eventuous.Diagnostics.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.AspNetCore;

[PublicAPI]
public static class LoggingAppBuilderExtensions {
    public static IHost AddEventuousLogs(this IHost host) {
        AddEventuousLogs(host.Services);
        return host;
    }

    public static void AddEventuousLogs(this IServiceProvider provider)
        => listener ??= new LoggingEventListener(provider.GetRequiredService<ILoggerFactory>());

    static LoggingEventListener? listener;
}
