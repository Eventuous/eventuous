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
    readonly WebApplicationBuilder _builder;

    LogEventLevel?                                      _minimumLogLevel;
    Func<LoggerSinkConfiguration, LoggerConfiguration>? _sinkConfiguration;
    Func<LoggerConfiguration, LoggerConfiguration>?     _configureLogger;

    internal ConnectorApplicationBuilder() {
        _builder = WebApplication.CreateBuilder();
        _builder.AddConfiguration();
        Config = _builder.Configuration.GetConnectorConfig<TSourceConfig, TTargetConfig>();
        _builder.Services.AddSingleton(Config.Source);
        _builder.Services.AddSingleton(Config.Target);

        if (!Config.Connector.Diagnostics.Enabled) {
            Environment.SetEnvironmentVariable("EVENTUOUS_DISABLE_DIAGS", "1");
        }
    }

    public ConnectorConfig<TSourceConfig, TTargetConfig> Config { get; }

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

    public ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> RegisterDependencies(
        Action<IServiceCollection, ConnectorConfig<TSourceConfig, TTargetConfig>> configure
    ) {
        configure(_builder.Services, Config);
        return this;
    }

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
        _builder.Services.AddConnector(builder => configure(builder, Config));
        return this;
    }

    const string ConnectorIdTag = "connectorId";

    public void EnrichActivity(Activity activity, string arg1, object arg2)
        => activity.AddTag(ConnectorIdTag, Config.Connector.ConnectorId);

    bool _otelAdded;

    public ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> AddOpenTelemetry(
        Action<TracerProviderBuilder, Action<Activity, string, object>>? configureTracing = null,
        Action<MeterProviderBuilder>?  configureMetrics = null
    ) {
        _otelAdded = true;

        if (!Config.Connector.Diagnostics.Enabled) {
            return this;
        }

        EventuousDiagnostics.AddDefaultTag(ConnectorIdTag, Config.Connector.ConnectorId);

        if (Config.Connector.Diagnostics.Trace) {
            _builder.Services.AddOpenTelemetryTracing(
                cfg => {
                    cfg
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Config.Connector.ServiceName))
                        .SetSampler(new TraceIdRatioBasedSampler(Config.Connector.Diagnostics.TraceSamplerProbability))
                        .AddEventuousTracing();

                    configureTracing?.Invoke(cfg, EnrichActivity);
                    cfg.AddOtlpExporter();
                }
            );
        }

        if (Config.Connector.Diagnostics.Metrics) {
            _builder.Services.AddOpenTelemetryMetrics(
                cfg => {
                    cfg.AddEventuous().AddEventuousSubscriptions();
                    configureMetrics?.Invoke(cfg);
                    if (Config.Connector.Diagnostics.Prometheus) cfg.AddPrometheusExporter();
                }
            );
        }

        return this;
    }

    public ConnectorApp Build() {
        _builder.ConfigureSerilog(_minimumLogLevel, _sinkConfiguration, _configureLogger);

        if (!_otelAdded) {
            AddOpenTelemetry();
        }

        var app = _builder.Build();

        if (Config.Connector.Diagnostics.Prometheus) {
            app.UseOpenTelemetryPrometheusScrapingEndpoint();
        }

        return new ConnectorApp(app);
    }
}

public class ConnectorApp {
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

    public static Task RunConnector<TSourceConfig, TTargetConfig>(
        this ConnectorApplicationBuilder<TSourceConfig, TTargetConfig> builder
    ) where TSourceConfig : class where TTargetConfig : class {
        var application = builder.Build();
        return application.Run();
    }
}
