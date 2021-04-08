---
title: "Event store"
description: "Event store"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "persistence"
weight: 310
toc: true
---

In order to isolate the core library from a particular way of storing events, Eventuous uses the `IEventStore` abstraction. Whilst it's used by `AggregateStore`, you can also use it in a more generic way, when you need to persist or read events without having an aggregate.

We have two implementations of event store:
- `EsdbEventStore` which uses [EventStoreDB](https://eventstore.com)
- In-memory store in the test project

### Primitives

Event store works with a couple of primitives, which allow wrapping infrastructure-specific structures. Those primitives are:

| Record type | What it's for |
| ----------- | ------------- |
| `StreamReadPosition` | Represent the stream revision, from there the event store will read the stream forwards or backwards. |
| `ExpectedStreamVersion` | The stream version (revision), which we expect to have in the database, when event store tries to append new events. Used for optimistic concurrency. |
| `StreamEvent` | A structure, which holds the event type as a string as well as serialised event payload and metadata. |

All of those are immutable records.

### Operations

Right now, we only have four operations for an event store:

| Function | What's it for |
| -------- | ------------- |
| `AppendEvents` | Append one or more events to a given stream. |
| `ReadEvents` | Read events from a stream forwards, from a given start position. |
| `ReadEventsBackwards` | Read events from a stream backwards, starting from the end of the stream. |
| `ReadStream` | Read events from a stream forwards asynchronously, calling a function provided as an argument for each event. |

For the parameters, you can look at the interface source code. If you use the EventStoreDB implementation we provide, you won't need to know about the event store abstraction. It is required though if you want to implement it for your preferred database.

{{% alert icon="👉" %}}
Preferring EventStoreDB will save you lots of time!
Remember to check [Event Store Cloud](https://www.eventstore.com/event-store-cloud).
{{% /alert %}}
