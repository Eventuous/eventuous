using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.Producers;
using Eventuous.Subscriptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Eventuous.Connectors.Base;

public class ConnectorApplicationBuilder<TSourceConfig, TTargetConfig>
    where TSourceConfig : class
    where TTargetConfig : class {
    LogEventLevel?                                      _minimumLogLevel;
    Func<LoggerSinkConfiguration, LoggerConfiguration>? _sinkConfiguration;
    Func<LoggerConfiguration, LoggerConfiguration>?     _configureLogger;

    internal ConnectorApplicationBuilder() {
        Builder = WebApplication.CreateBuilder();
        Builder.AddConfiguration();
        Config = Builder.Configuration.GetConnectorConfig<TSourceConfig, TTargetConfig>();
        Builder.Services.AddSingleton(Config.Source);
        Builder.Services.AddSingleton(Config.Target);

        if (!Config.Connector.Diagnostics.Enabled) {
            Environment.SetEnvironmentVariable("EVENTUOUS_DISABLE_DIAGS", "1");
        }
    }

    [PublicAPI]
    public WebApplicationBuilder Builder { get; }
    [PublicAPI]
    public ConnectorConfig<TSourceConfig, TTargetConfig> Config { get; }

    [PublicAPI]
    public ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> ConfigureSerilog(
        LogEventLevel?                                      minimumLogLevel   = null,
        Func<LoggerSinkConfiguration, LoggerConfiguration>? sinkConfiguration = null,
        Func<LoggerConfiguration, LoggerConfiguration>?     configure         = null
    ) {
        _minimumLogLevel   = minimumLogLevel;
        _sinkConfiguration = sinkConfiguration;
        _configureLogger   = configure;
        return this;
    }

    [PublicAPI]
    public ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> RegisterDependencies(
        Action<IServiceCollection, ConnectorConfig<TSourceConfig, TTargetConfig>> configure
    ) {
        configure(Builder.Services, Config);
        return this;
    }

    [PublicAPI]
    public ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> RegisterConnector<TSubscription,
        TSubscriptionOptions, TProducer, TProduceOptions>(
        Func<ConnectorBuilder, ConnectorConfig<TSourceConfig, TTargetConfig>,
                ConnectorBuilder<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>>
            configure
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TSubscriptionOptions : SubscriptionOptions
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class {
        Builder.Services.AddConnector(builder => configure(builder, Config));
        return this;
    }

    const string ConnectorIdTag = "connectorId";

    void EnrichActivity(Activity activity, string arg1, object arg2)
        => activity.AddTag(ConnectorIdTag, Config.Connector.ConnectorId);

    bool _otelAdded;

    [PublicAPI]
    public ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> AddOpenTelemetry(
        Action<TracerProviderBuilder, Action<Activity, string, object>>? configureTracing = null,
        Action<MeterProviderBuilder>?                                    configureMetrics = null,
        Sampler?                                                         sampler          = null,
        ExporterMappings<TracerProviderBuilder>?                         tracingExporters = null,
        ExporterMappings<MeterProviderBuilder>?                          metricsExporters = null
    ) {
        _otelAdded = true;

        if (!Config.Connector.Diagnostics.Enabled) {
            return this;
        }

        EventuousDiagnostics.AddDefaultTag(ConnectorIdTag, Config.Connector.ConnectorId);

        if (Config.Connector.Diagnostics.Tracing is { Enabled: true }) {
            Builder.Services.AddOpenTelemetryTracing(
                cfg => {
                    cfg.AddEventuousTracing();

                    configureTracing?.Invoke(cfg, EnrichActivity);

                    cfg
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Config.Connector.ServiceName))
                        .SetSampler(
                            sampler ?? new TraceIdRatioBasedSampler(
                                Config.Connector.Diagnostics.TraceSamplerProbability
                            )
                        );

                    tracingExporters?.RegisterExporters(cfg, Config.Connector.Diagnostics.Tracing.Exporters);
                }
            );
        }

        if (Config.Connector.Diagnostics.Metrics is { Enabled: true }) {
            Builder.Services.AddOpenTelemetryMetrics(
                cfg => {
                    cfg.AddEventuous().AddEventuousSubscriptions();
                    configureMetrics?.Invoke(cfg);
                    cfg.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Config.Connector.ServiceName));
                    metricsExporters?.RegisterExporters(cfg, Config.Connector.Diagnostics.Metrics.Exporters);
                }
            );
        }

        return this;
    }

    public ConnectorApp Build() {
        Builder.ConfigureSerilog(_minimumLogLevel, _sinkConfiguration, _configureLogger);

        if (!_otelAdded) {
            AddOpenTelemetry();
        }

        var app = Builder.Build();
        return new ConnectorApp(app);
    }
}

public class ConnectorApp {
    [PublicAPI]
    public static ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> Create<TSourceConfig, TTargetConfig>()
        where TSourceConfig : class where TTargetConfig : class
        => new();

    public WebApplication Host { get; }

    internal ConnectorApp(WebApplication host) => Host = host;

    public async Task<int> Run() {
        try {
            await Host.RunConnector();
            return 0;
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Host terminated unexpectedly");
            return -1;
        }
        finally {
            Log.CloseAndFlush();
        }
    }
}

public static class ConnectorBuilderExtensions {
    [PublicAPI]
    public static Task RunConnector<TSourceConfig, TTargetConfig>(
        this ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> builder
    ) where TSourceConfig : class where TTargetConfig : class {
        var application = builder.Build();
        return application.Run();
    }
}
