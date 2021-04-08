---
title: "Concept"
description: "The concept of read models"
date: 2021-04-08
lastmod: 2021-04-08
draft: false
images: []
menu:
  docs:
    parent: "read-models"
weight: 620
toc: true
---

## Writes and writes

As described [previously]({{< ref "aggregate" >}}), the domain model is using [events](../../domain/events) as the _source of truth_. These events represent individual and atomic state transitions of the system. We add events to [event store](../../persistence/event-store) one by way, in append-only fashion. When restoring the state of an aggregate, we read all the events from a single stream, and apply those events to the aggregate state. When all events are applied, the state is fully restored. This process takes nanoseconds to complete, so it's not really a burden.
