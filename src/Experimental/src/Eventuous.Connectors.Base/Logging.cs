using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Eventuous.Connectors.Base;

public static class Logging {
    public static void ConfigureSerilog(
        this WebApplicationBuilder                          builder,
        LogEventLevel?                                      minimumLogLevel   = null,
        Func<LoggerSinkConfiguration, LoggerConfiguration>? sinkConfiguration = null,
        Func<LoggerConfiguration, LoggerConfiguration>?     configure         = null
    ) {
        var sc = sinkConfiguration ?? DefaultSink;

        var logLevel = minimumLogLevel
                    ?? (builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information);

        var logConfig = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Grpc", LogEventLevel.Fatal)
            .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
            .Enrich.FromLogContext();

        logConfig = configure?.Invoke(logConfig) ?? logConfig;

        Log.Logger = sc(logConfig.WriteTo).CreateLogger();

        builder.Host.UseSerilog();

        LoggerConfiguration DefaultSink(LoggerSinkConfiguration sinkConfig)
            => builder.Environment.IsDevelopment()
                ? sinkConfig.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>;{NewLine}{Exception}"
                )
                : sinkConfig.Console(new RenderedCompactJsonFormatter());
    }
}
