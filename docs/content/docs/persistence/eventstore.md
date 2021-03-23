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
weight: 620
toc: true
---

In order to isolate the core library from a particular way of storing events, Eventuous uses the `IEventStore` abstraction. Whilst it's used by `AggregateStore`, you can also use it in a more generic way, when you need to persist or read events without having an aggregate.

We have two implementations of event store:
- `EsdbEventStore` which uses [EventStoreDB](https://eventstore.com)
- In-memory store in the test project

### Primitives

Event store works with a couple of primitives, which allow wrapping infrastructure-specific structures. Those primitives are:

- `StreamReadPosition` - represent the stream revision, from there the event store will read the stream forwards or backwards.
- `ExpectedStreamVersion` - the stream version (revision), which we expect to have in the database, when event store tries to append new events. Used for optimistic concurrency.
- `StreamEvent` - a structure, which holds the event type as a string as well as serialised event payload and metadata.

### Operations

- `AppendEvents` - append one or more events to the stream
- `ReadEvents` - read events from a stream forwards, from a given start position
- `ReadEventsBackwards` - read events from a stream backwards, starting from the end of the stream
- `ReadStream` - read events from a stream forwards asynchronously, calling a function provided as an argument for each event
