---
title: "Configuration and deployment"
description: "How to configure and deploy Eventuous Connector"
sidebar_position: 2
---

Eventuous Connector with EventStoreDB source needs to be hosted as a continuously running service because it must maintain a realtime gRPC subscription. When you deploy a projector, it can be deployed as a sidecar for the connector, or as a standalone service. It could be possible to deploy it as a serverless workload if the serverless solution supports gRPC streaming.

## Deployment

You can use the Connector container to available on [Docker Hub](https://hub.docker.com/repository/docker/eventuous/connector) in Kubernetes, Docker Compose, or serverless environment like Google CLoud Run or Amazon Fargate. If you want to use the Connector in projector mode, we recommend deploying your gRPC server as a sidecar for the connector.

When running the Connector in a serverless environment, you'd need to set the minimal and maximum container count to one. Remember that the Connector should run continuously, and if you run more than one instance simultaneously, you risk getting unexpected side effects.

> More information will be available soon.

## Configuration

To configure the Connector, you need to have a `config.yaml` file in the same directory as the Connector. The file consists of three sections: 

- `connector`: This section contains the configuration for the Connector.
- `source`: This section contains the configuration for the EventStoreDB source database.
- `target`: This section contains the configuration for the target database or broker.

The target configuration is specific to the target type. Find out more on the specific connector page in the left navigation menu.

### Connector configuration

Here's an example of a Cconnector section in the configuration file:

```yaml
connector:
  connectorId: "esdb-elastic-connector"
  connectorAssembly: "Eventuous.Connector.EsdbElastic"
  diagnostics:
    tracing:
      enabled: true
      exporters: [zipkin]
    metrics:
      enabled: true
      exporters: [prometheus]
    traceSamplerProbability: 0
```

The connector id is used as the Eventuous subscription id. If the target is using a checkpoint store, this value will also be used as the checkpoint id. When running multiple connectors with different targets, make sure that each connector has its own unique id.

The assembly name is needed so the Connector can load the specific target implementation. All the target assemblies are included to the container by default, but only the specified assembly is loaded at runtime.

### Diagnotics configuration

The connector is fully instrumented with traces and metrics. The following configuration parameters are supported:

* `enabled` - if diagnostics are enabled
* `tracing` - the tracing configuration
    * `enabled` - if tracing is enabled
    * `exporters` - the tracing exporters (zipkin, jaeger, otpl)
* `metrics` - the metrics configuration
    * `enabled` - if metrics are enabled
    * `exporters` - the metrics exporters (prometheus, otpl)

#### Metrics exporters

- `otlp`: Exports metrics using OpenTelemetry protocol. You need to configure the exporter using environment variables as [described in the documentation][1].
- `prometheus`: Adds the Prometheus metrics endpoint at `/metrics` path.

#### Trace exporters

- `otlp`: Exports traces using OpenTelemetry protocol. You need to configure the exporter using environment variables as [described in the documentation][1].
- `zipkin`: Exports traces to Zipkin. You can configure the exporter using environment variables as [described in the documentation][2].
- `jaeger`: Exports traces to Jaeger. You can configure the exporter using environment variables as [described in the documentation][2].

### Source configuration

The source configuration is used to connect to the EventStoreDB, as well as configure the subscription. At the moment, the connector will unconditionally subscribe to `$all` stream.

The following configuration parameters are supported:
* `connectionString` - EventStoreDB connection string using gRPC protocol. For example: `esdb://localhost:2113?tls=false`
* `concurrencyLimit` - the subscription concurrency limit. The default value is `1`.

```yaml
source:
    connectionString: "esdb://localhost:2113?tls=false"
    concurrencyLimit: 1
```

When the subscription concurrency limit is higher than `1`, the subscription will partition events between multiple Elasticsearch producer instances. As those producers will run in parallel, it will increase the overall throughput.


[1]: https://opentelemetry.io/docs/reference/specification/protocol/exporter/
[2]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/d93606ea71d0d124592b3fc60f0388b5701591de/src/OpenTelemetry.Exporter.Jaeger/README.md#environment-variables
[3]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/d93606ea71d0d124592b3fc60f0388b5701591de/src/OpenTelemetry.Exporter.Jaeger/README.md#environment-variables
