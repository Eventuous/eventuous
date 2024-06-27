---
title: "Generic gRPC"
description: "Send events to gRPC endpoint"
sidebar_position: 1
---

The gRPC sink allows you to send events to any gRPC endpoint. You are then free to do anything you want with the event, like projecting it to another database, sending it to a message broker, or even sending it to another EventStoreDB instance.

The Connector takes care about subscribing to the source (EventStoreDB) and sending events to your gRPC endpoint in order, possibly partitioned, and with retries. It also arranges the checkpoints and report the checkpoint to your service, so you can update it in your database.

## Endpoint implementation

The Connector expects to talk to your gRPC server, implemented using the following protocol:

```proto
syntax = "proto3";

package grpc_projection;
import "google/protobuf/struct.proto";

service Projection {
  rpc Project (ProjectRequest) returns (ProjectResponse);
  rpc GetCheckpoint (GetCheckpointRequest) returns (GetCheckpointResponse);
  rpc StoreCheckpoint (StoreCheckpointRequest) returns (StoreCheckpointResponse);
}

message ProjectedEvent {
  string eventType = 1;
  string eventId = 2;
  string stream = 3;
  int32 eventNumber = 4;
  int64 position = 5;
  google.protobuf.Struct eventPayload = 6;
  map<string, string> metadata = 7;
}

message ProjectRequest {
  repeated ProjectedEvent events = 1;
}

message ProjectResponse {
}

message GetCheckpointRequest {
  string checkpointId = 1;
}

message GetCheckpointResponse {
  oneof checkpoint {
    int64 position = 1;
  }
}

message StoreCheckpointRequest {
  string checkpointId = 1;
  int64 position = 2;
}

message StoreCheckpointResponse {
}
```

Essentially, there are three operations to implement in the server:

- `LoadCheckpoint`: get the checkpoint for the given `checkpointId`.
- `StoreCheckpoint`: store the checkpoint for the given `checkpointId`.
- `Project`: project the given events to your database.

You'd normally store the checkpoint in the same database where you project events (read model database). If you aren't familiar with the checkpoint concept, you can learn more about it in the [Checkpointing](../../../subscriptions/checkpoint) section of Eventuous documentation.

Here's an example of a simple gRPC server implementation in C#:

```csharp
using Grpc.Core;

namespace GrpcProjector.Services;

public class ProjectorService : Projection.ProjectionBase {
    readonly ILogger<ProjectorService> _logger;

    public ProjectorService(ILogger<ProjectorService> logger) => _logger = logger;

    public override Task<GetCheckpointResponse> GetCheckpoint(
        GetCheckpointRequest request, 
        ServerCallContext context
    ) {
        _logger.LogInformation("Loading checkpoint");

        return Task.FromResult(new GetCheckpointResponse { Position = 0 });
    }

    public override Task<StoreCheckpointResponse> StoreCheckpoint(
        StoreCheckpointRequest request, 
        ServerCallContext context
    ) {
        _logger.LogInformation("Storing checkpoint {Position}", request.Position);

        return Task.FromResult(new StoreCheckpointResponse());
    }

    public override Task<ProjectResponse> Project(ProjectRequest request, ServerCallContext context) {
        foreach (var evt in request.Events) {
            _logger.LogDebug(
                "Projecting event {Type} at {Stream}:{Position}", 
                evt.EventType, evt.Stream, evt.EventNumber
            );
            Console.WriteLine(evt.EventPayload.ToString());
        }

        return Task.FromResult(new ProjectResponse());
    }
}
```

This example doesn't execute any database operations, but you can easily add them to the `Project` method.

The full sample is available in the [Connector repository](https://github.com/Eventuous/connectors/tree/a96c6f905a21b386c5961055a9743a7a7de20764/samples/GrpcProjector).

You can deploy the gRPC server as a sidecar to the Connector workload, or as a standalone service, also in a serverless environment that supports HTTP/2 like Google Cloud Run. Connector will connect to the server using the `uri` and `credentials` configuration options. The authorization header is not supported yet.

## Configuration

The Connector configuration has the same basic options as for other sinks, except the `target` option, which needs to point to your gRPC server:

```yaml
connector:
  connectorId: "esdb-grpc-connector"
  connectorAssembly: "Eventuous.Connector.EsdbGenericGrpc"
  diagnostics:
    tracing:
      enabled: true
      exporters: [zipkin]
    metrics:
      enabled: true
      exporters: [prometheus]
    traceSamplerProbability: 0
source:
  connectionString: "esdb://localhost:2113?tls=false"
  concurrencyLimit: 10
target:
  uri: "https://localhost:5001"
  credentials: "ssl"
```