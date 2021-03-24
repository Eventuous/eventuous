---
title: "Aggregate store"
description: "Aggregate store"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "persistence"
weight: 320
toc: true
---

{{< alert icon="👉" text="Eventuous does not have a concept of Repository. Find out Why at the bottom of this page." >}}

Eventuous provides a single abstraction for the domain objects persistence, which is the `AggregateStore`.

The `AggregateStore` uses the `IEventStore` abstraction to be persistence-agnostic, so it can be used as-is, when you give it a proper implementation of event store.

We have only two operations in the `AggegateStore`:
- `Load` - retrieves events from an aggregate stream and restores the aggregate state using those events.
- `Store` - collects new events from an aggregate and stores those events to the aggregate stream.

Our `ApplicationService` uses the `AggregateStore` in its command-handling flow.
