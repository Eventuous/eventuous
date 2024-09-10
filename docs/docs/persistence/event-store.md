---
title: "Event store"
description: "Event store infrastructure"
sidebar_position: 2
---

In order to isolate the core library from a particular way of storing events, Eventuous uses the `IEventStore` abstraction. Whilst it's used by `AggregateStore`, you can also use it in a more generic way, when you need to persist or read events without having an aggregate.

The `IEventStore` interface inherits from `IEventReader` and `IEventWriter` interfaces. Each of those interfaces is focused on one specific task - reading events from streams, and appending events to streams. This separation is necessary for scenarios when you only need, for example, to read events from a specific store, but not to append them. In such case, you'd want to use the `IEventReader` interface only.

Eventuous has several implementations of event store abstraction, you can find them in the [infrastructure](../infra) section. The default implementation is `EsdbEventStore`, which uses [EventStoreDB](https://eventstore.com) as the event store. It's a great product, and we're happy to provide first-class support for it. It's also a great product for learning about Event Sourcing and CQRS.

In addition, Eventuous has an in-memory event store, which is mostly used for testing purposes. It's not recommended to use it in production, as it doesn't provide any persistence guarantees.

### Primitives

Event store works with a couple of primitives, which allow wrapping infrastructure-specific structures. Those primitives are:

| Record type             | What it's for                                                                                                                                         |
|-------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| `StreamReadPosition`    | Represent the stream revision, from there the event store will read the stream forwards or backwards.                                                 |
| `ExpectedStreamVersion` | The stream version (revision), which we expect to have in the database, when event store tries to append new events. Used for optimistic concurrency. |
| `StreamEvent`           | A structure, which holds the event type as a string as well as serialised event payload and metadata.                                                 |

All of those are immutable records.

### Operations

Right now, we only have four operations for an event store:

| Function              | What's it for                                                                                                 |
|-----------------------|---------------------------------------------------------------------------------------------------------------|
| `AppendEvents`        | Append one or more events to a given stream.                                                                  |
| `ReadEvents`          | Read events from a stream forwards, from a given start position.                                              |

Eventuous has several implementations of the event store: 
 * [EventStoreDB](../infra/esdb)
 * [PostgreSQL](../infra/postgres)
 * [Microsoft SQL Server](../infra/mssql)
 * [Elasticsearch](../infra/elastic) 

If you use one of the implementations provided, you won't need to know about the event store abstraction. It is required though if you want to implement it for your preferred database. 

:::tip
Preferring EventStoreDB will save you lots of time!
Remember to check [Event Store Cloud](https://www.eventstore.com/event-store-cloud).
:::
