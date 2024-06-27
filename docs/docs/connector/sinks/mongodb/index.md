---
title: "MongoDB"
description: "Project events from EventStoreDB to MongoDB"
sidebar_position: 2
---

MongoDB sink only support the `projector` mode for now.

You need to run a gRPC server (sidecar or a standalone workload) accessible to the Connector to make the sink work.

## Quick start

You can check the samples of MongoDB sidecars:
- [NodeJS][1]
- [PHP][2]

Both samples contain the following:
- Code generated from proto-files by language-specific Protobuf transpiler
- MongoDB-oriented projections DSL for some of the supported operations
- Bootstrap code for the gRPC server
- Actual projections for events produced by the [sample app](https://github.com/Eventuous/dotnet-sample)

Those examples can help you to understand the concept and implement your own projectors.

## Projector sidecar

You can create the projector gRPC server using any language or stack. The server must support bidirectional streaming, and implement the gRPC sidecar interface as described on the [gRPC projector](../../projectors/grpc) page. The `Project` operation of the `Projection` server returns the `ProjectionResponse` message, where the `operation` field should be set to one of the following MongoDB-specific responses:

```proto
syntax = "proto3";

package projection;

import "google/protobuf/struct.proto";

message InsertOne {
  google.protobuf.Struct document = 1;
}

message InsertMany {
  repeated google.protobuf.Struct documents = 1;
}

message UpdateOne {
  google.protobuf.Struct filter = 1;
  google.protobuf.Struct update = 2;
}

message UpdateMany {
  google.protobuf.Struct filter = 1;
  google.protobuf.Struct update = 2;
}

message DeleteOne {
  google.protobuf.Struct filter = 1;
}

message DeleteMany {
  google.protobuf.Struct filter = 1;
}
```

## Configuration

There are two sections to configure in the [Connector configuration](../../deployment/#configuration): `target` and `grpc`. The `target` section specified the MongoDB configuration, and the `grpc` section contains the sidecar URL.

For the MongoDB target, you need to configure the following parameters:

- `connectionString`: The connection string to the MongoDB instance.
- `database`: The name of the database to use.
- `collection`: The name of the collection to use.

You can only project to one collection in one database using a single Connector instance.

Here's the sample configuration for this connector:

```yaml
connector:
  connectorId: "esdb-mongo-connector"
  connectorAssembly: "Eventuous.Connector.EsdbMongo"
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
  concurrencyLimit: 1
target:
  connectionString: "mongodb://mongoadmin:secret@localhost:27017"
  database: test
  collection: bookings
grpc:
  uri: "http://localhost:9091"
  credentials: "insecure"
```

[1]: https://github.com/Eventuous/connector-sidecar-nodejs-mongo
[2]: https://github.com/Eventuous/connector-sidecar-php-mongo
