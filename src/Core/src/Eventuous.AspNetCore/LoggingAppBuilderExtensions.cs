using Eventuous.Diagnostics.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.AspNetCore; 

[PublicAPI]
public static class LoggingAppBuilderExtensions {
    public static IHost AddEventuousLogs(this IHost host) {
        if (_listener != null)
            _listener = new LoggingEventListener(host.Services.GetRequiredService<LoggerFactory>());

        return host;
    }

    static LoggingEventListener? _listener;
}