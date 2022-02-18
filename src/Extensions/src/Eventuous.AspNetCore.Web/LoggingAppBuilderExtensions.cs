using Eventuous.Diagnostics.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Eventuous.AspNetCore.Web;

[PublicAPI]
public static class LoggingAppBuilderExtensions {
    public static IApplicationBuilder UseEventuousLogs(this IApplicationBuilder host) {
        listener ??= new LoggingEventListener(host.ApplicationServices.GetRequiredService<ILoggerFactory>());
        return host;
    }

    static LoggingEventListener? listener;
}
