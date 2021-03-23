---
title: "Subscriptions"
description: "Answers to frequently asked questions."
lead: "Answers to frequently asked questions."
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "subscriptions"
weight: 630
toc: true
---

## Concept

One of the common mistakes people make when building an event-sourced application is to use an event store, which is not capable of handling realtime subscriptions. It forces developers to engage some sort of message bus to deliver new events to subscribers. There are quite a few issues with that approach, but the most obvious one is a two-phase commit.

When using two distinct pieces of infrastructure in one transaction, you risk one of those operations to fail. Let's use the following example code, which is very common:

```csharp
await _repository.Save(newEvents);
await _bus.Publish(newEvents);
```

If the second operation fails, the command side of the application would remain consistent. However, any read models, which projects those events, will not be updated. So, essentially, the reporting model will become inconsistent against the transactional model. The worst part is that the reporting model will never recover from the failure.

As mentioned, there are multiple issues of using a message bus as transport to deliver events to reporting models, but we won't be covering them on this page.

The easiest way to solve the issue is to use a database, which supports realtime subscriptions to event streams out of the box. That's why we use EventStoreDB as the primary event store implementation.

## Subscriptions

Most of the time, subscriptions are used for two purposes:
1. Deliver events to reporting models
1. Emit integration events

In Eventuous, subscriptions are specific to the event store. We currently only provide subscriptions for EventStoreDB.

There are two types of subscriptions available:
- Stream subscription
- All stream subscription

TBD

- Hosted service
- Serialiser
- Type map
