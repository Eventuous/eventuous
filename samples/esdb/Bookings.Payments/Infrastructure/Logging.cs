using Serilog;
using Serilog.Events;

namespace Bookings.Payments.Infrastructure;

public static class Logging {
    public static void ConfigureLog()
        => Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Information)
            .MinimumLevel.Override("EventStore", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}"
            )
            .CreateLogger();
}
