---
title: "MS SQL Server"
description: "Project events from EventStoreDB to MS SQL Server or Azure SQL"
---

SQL Server sink only support the `projector` mode.

You need to run a gRPC server (sidecar or a standalone workload) accessible to the Connector to make the sink work.

## Quick start

You can check the sample of SQL Server sidecar using [NodeJS][1].

Both samples contain the following:
- Code generated from proto-files by language-specific Protobuf transpiler
- MongoDB-oriented projections DSL for some of the supported operations
- Bootstrap code for the gRPC server
- Actual projections for events produced by the [sample app](https://github.com/Eventuous/dotnet-sample)

Those examples can help you to understand the concept and implement your own projectors.

## Projector sidecar

You can create the projector gRPC server using any language or stack. The server must support bidirectional streaming, and implement the gRPC sidecar interface as described on the [gRPC projector](../../projectors/grpc) page. The `Project` operation of the `Projection` server returns the `ProjectionResponse` message, where the `operation` field should contain a SQL statement to execute:

```proto
syntax = "proto3";

package projection;

option csharp_namespace = "Eventuous.Connector.EsdbSqlServer";

message Execute {
  string sql = 1;
}
```

For example (using TypeScript), with a helper function `execute` defined as:

```typescript
import {AnyEventHandlerMap, project, WrapToAny} from "./common";
import {Execute} from "./compiled/proto/project";

export const execute = <T>(eventType: string, handler: (event: T) => string): AnyEventHandlerMap =>
    project<T>(
        eventType,
        e => {
            const update = {sql: handler(e)};
            return Execute.fromPartial(update);
        });
```

You can return SQL inserts or updates based on different event types:

```typescript
import {Projector} from "./common";
import {execute} from "./sqlProjectors";
import {PaymentRegistered, RoomBooked} from "./eventTypes";

export const sqlProjections: Projector = [
    execute<RoomBooked>(
        "V1.RoomBooked",
        event => 
            `INSERT INTO Bookings (BookingId, RoomId, GuestId) 
            VALUES ('${event.bookingId}', '${event.roomId}', '${event.guestId}')`
    ),
    execute<PaymentRegistered>(
        "V1.PaymentRegistered",
        event => 
            `UPDATE Bookings 
            SET OutstandingAmount = ${event.amount} 
            WHERE BookingId = '${event.bookingId}'`
    )
];
```

## Configuration

There are two sections to configure in the [Connector configuration](../../deployment/#configuration): `target` and `grpc`. The `target` section specified the SQL Server configuration, and the `grpc` section contains the sidecar URL (your service).

For the SQL Server sink, you need to configure the following parameters:

- `connectionString`: The connection string to SQL Server, including all the credentials and the database name

You can only project to one database using a single Connector instance.

Here's the sample configuration for this connector:

```yaml
connector:
  connectorId: "esdb-sql-connector"
  connectorAssembly: "Eventuous.Connector.EsdbSqlServer"
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
    connectionString: "Server=localhost;Database=test;User=sa;Password=Your_password123;Encrypt=True;TrustServerCertificate=True"
grpc:
  uri: "http://localhost:9091"
  credentials: "insecure"
```

[1]: https://github.com/Eventuous/connector-sidecar-nodejs-sqlserver
