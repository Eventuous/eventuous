# Metrics

Metrics are collected using several `Meter` instances. There are two available meters:

- `eventuous.application` for command services
- `eventuous.subscriptions` for subscriptions
- `eventuous.persistence` for event stores

Producers do not provide meters, but you can use traces to collect diagnostics for producers.

## Application metrics

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

## Persistence metrics

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

## Subscription metrics

Subscription metrics are described in the [subscriptions diagnostics](../subscriptions/subs-diagnostics/index.md#subscription-metrics) page.

