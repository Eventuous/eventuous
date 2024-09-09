# Diagnostics in details

Eventuous provides built-in metrics and traces for:
- Event store
- Subscriptions, consumers, and event handlers
- Command services
- Producers

The built-in diagnostics integrate with [OpenTelemetry](https://opentelemetry.io/) using [OpenTelemetry .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel).

## Enabling diagnostics

Diagnostic instrumentation is enabled by default. You can disable it by setting the `EVENTUOUS_DISABLE_DIAGS` environment variable to any value except `1`. It is also possible to disable diagnostics at runtime by calling `EventuousDiagnostics.Disable()` static function.

When diagnostics are enabled, registering different Eventuous elements will wrap them in diagnostic decorators. The decorators collect metrics and traces for the registered elements. For example, when registering an event reader using `AddEventReader` extension, the provided event reader type will be used by `TracedEventReader` decorator, which collects metrics and traces for the event reader.

## Logging

Eventuous uses the ASP.NET Core logging with `ILoggerFactory` and `ILogger<T>`, so you can use the standard logging facilities to log diagnostics. For internal logging, Eventuous uses multiple [event sources](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource), which you can collect and analyse with tools like `dotnet-trace`.

It's also possible to expose internal Eventuous logs using the `UseEventuousLogs` extension for `IHost`, which is available as part of `Eventuous.Extensions.DependencyInjection` package.

## Metrics

Metrics are collected using several `Meter` instances. There are two available meters:

- `eventuous.application` for command services
- `eventuous.subscriptions` for subscriptions
- `eventuous.persistence` for event stores

Producers do not provide meters, but you can use traces to collect diagnostics for producers.

### Application metrics

Application metrics are collected for command services. The metrics are collected for the duration and error count of command processing. The metrics are tagged by:

- `command-service`: the service type
- `command-type`: command type

Command handling duration is collected as a histogram with the name `eventuous_service_duration` with measure unit `milliseconds`. The number of errors that occurred when handling commands is collected as a counter with the name `eventuous_service_errors_count`.

Here's an example of command service metrics exported in Prometheus format:

```prometheus
# TYPE eventuous_service_duration_milliseconds histogram
# UNIT eventuous_service_duration_milliseconds milliseconds
# HELP eventuous_service_duration_milliseconds Command execution duration, milliseconds
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="0"} 0 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="5"} 0 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="10"} 0 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="25"} 0 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="50"} 0 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="75"} 0 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="100"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="250"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="500"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="750"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="1000"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="2500"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="5000"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="7500"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="10000"} 1 1725881998415
eventuous_service_duration_milliseconds_bucket{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom",le="+Inf"} 1 1725881998415
eventuous_service_duration_milliseconds_sum{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom"} 86.583 1725881998415
eventuous_service_duration_milliseconds_count{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom"} 1 1725881998415
# TYPE eventuous_service_errors_count_total counter
# UNIT eventuous_service_errors_count_total errors
# HELP eventuous_service_errors_count_total Number of failed commands
eventuous_service_errors_count_total{otel_scope_name="eventuous.application",otel_scope_version="0.15.0.0",command_service="BookingsCommandService",command_type="BookRoom"} 1 1725881998415
```

### Persistence metrics

When Eventuous diagnostics is enabled (by default), registering any persistence component like event reader, writer or store will wrap it in a diagnostic decorator. The decorator collects persistence metrics. The metrics are tagged by:

- `operation`: the operation type (`append`, `read`, etc)
- `component`: the persistence implementation type, for example `EsdbEventStore`

Persistence operation duration is collected as a histogram with the name `eventuous_persistence_duration` with measure unit `milliseconds`. The number of errors that occurred when executing persistence operations is collected as a counter with the name `eventuous_persistence_errors_count`.

Here's an example of persistence metrics exported in Prometheus format:

```prometheus
# TYPE eventuous_persistence_duration_milliseconds histogram
# UNIT eventuous_persistence_duration_milliseconds milliseconds
# HELP eventuous_persistence_duration_milliseconds Event store operation duration, milliseconds
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="0"} 0 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="5"} 0 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="10"} 0 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="25"} 0 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="50"} 0 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="75"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="100"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="250"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="500"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="750"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="1000"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="2500"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="5000"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="7500"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="10000"} 1 1725888603727
eventuous_persistence_duration_milliseconds_bucket{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append",le="+Inf"} 1 1725888603727
eventuous_persistence_duration_milliseconds_sum{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append"} 66.672 1725888603727
eventuous_persistence_duration_milliseconds_count{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append"} 1 1725888603727
# TYPE eventuous_persistence_errors_count_total counter
# UNIT eventuous_persistence_errors_count_total errors
# HELP eventuous_persistence_errors_count_total Number of failed event store operations
eventuous_persistence_errors_count_total{otel_scope_name="eventuous.persistence",otel_scope_version="0.15.0.0",component="EsdbEventStore",operation="append"} 1 1725888603727
```

### Subscription metrics

Subscription metrics are described in the [subscriptions diagnostics](../subscriptions/subs-diagnostics/index.md#subscription-metrics) page.

## Tracing

Eventuous uses .NET `Activity` API to trace operations for command services, persistence, subscriptions, and producers. When OpenTelemetry integration is enabled, the traces are exported to the configured exporter.

### Command service tracing

Command handling operation creates a span with the name that is a combination of the command service name and the command type. For example, a service called `BookingsCommandService` processing a command `BookRoom` would create a span `service.bookingscommandservice/bookroom`. The span is tagged with the command name attribute as well. The tag name is `eventuous.command`.

If the command handler fails, the span will be set as failed and the exception will be attached to the span.

### Persistence tracing

Persistence operations create a span with the name that is a combination of the operation name and the stream name. For example, appending events to the stream `Booking-128` would create a span `eventstore.append/booking-128`. The span is tagged with the operation name and the stream name attributes. The tag names are `db.operation` and `eventuous.stream`. In addition, the `db.system` tag is set to `eventstore`.

If the operation fails, the span will be set as failed and the exception will be attached to the span.

### Producer tracing

Producers operations create a span with the name `produce`. The span is tagged with the following attributes:

* `eventuous.stream`: name of the stream, topic or exchange where the message is produced to
* `message_type`: type of the message, only when one message is produced
* `message_id`: ID of the message, only when one message is produced
* `messaging_destination`: same value as `eventuous.stream`
* `messaging.message_id`: same value as `message_id`
* `messaging.destination_kind`: type of the destination, for example `stream`, `exchange`, etc.
* `messaging.system`: name of the messaging system, for example `rabbitmq`, `eventstoredb`, etc.
* `messaging.operation`: operation name, for example `produce`, `append`, etc.

### Subscription tracing

Subscription tracing is described in the [subscriptions diagnostics](../subscriptions/subs-diagnostics/index.md#subscription-tracing) page.

## OpenTelemetry integration

Eventuous uses OpenTelemetry .NET to collect and export metrics and traces. The integration requires `Eventuous.Diagnostics.OpenTelemetry` package to be installed. The package provides extensions for OpenTelemetry .NET hosting and configuration.

### Adding metrics

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

### Adding traces

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