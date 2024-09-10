# OpenTelemetry integration

Eventuous uses OpenTelemetry .NET to collect and export metrics and traces. The integration requires `Eventuous.Diagnostics.OpenTelemetry` package to be installed. The package provides extensions for OpenTelemetry .NET hosting and configuration.

## Adding metrics

Use two extensions for `MetricsProviderBuilder` to add Eventuous metrics:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(
        builder => {
            builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
                .AddEventuous()
                .AddEventuousSubscriptions()
                .AddPrometheusExporter();
        }
    );
```

* `AddEventuous` adds application and persistence metrics
* `AddEventuousSubscriptions` adds subscription metrics

## Adding traces

Use `AddEventuousTracing` extension to `TracerProviderBuilder` to add Eventuous traces:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(
        builder => {
            builder
                .AddEventuousTracing()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
                .SetSampler(new AlwaysOnSampler())
                .AddZipkinExporter();
        }
    );
```

Because all Eventuous traces use the same activity source, using `AddEventuousTracing` that connects OpenTelemetry .NET with the activity source will automatically collect traces for all Eventuous components.
