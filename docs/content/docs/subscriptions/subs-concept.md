---
title: "Real-time subscription"
description: "The concept of real-time subscriptions"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "subscriptions"
weight: 510
toc: true
---

Real-time subscriptions are essential when building a successful event-sourced system. They support the following sequence of operations:

- Domain model emits new event
- The event is stored to the event store
- The command is now handled, and the transaction is complete
- All the [read models]({{< ref "rm-concept" >}}) get the new event _from the store_ and project it if necessary

Most of the time, subscriptions are used for two purposes:
1. Deliver events to reporting models
1. Emit integration events

Subscriptions can subscribe from any position of a stream, but normally you'd subscribe from the beginning of the stream, which allows you to process all the historical events. For integration purposes, however, you would usually subscribe from _now_, as emitting historical events to other systems might produce undesired side effects.

One subscription can serve multiple event handlers. Normally, you would identify a group of event handlers, which need to be triggered together, and group them within one subscription. This way you avoid situations when, for example, two real models get updated at different speed and show confusing information if those read models are shown on the same screen.

{{% alert icon="😱" %}}
**Example (real life horror story)**

Imagine an event stream, which contains positions of a car as well as the car state changes, like `Parked`, `Moving` or `Idle`. The system also has two independent subscriptions serve two read model projections - one is the last know car location, and the other one is the car state. Those subscriptions will, with great probability, come out of sync, simply because position events are much more frequent than state updates. When using both of those read models on the map, you easily get a situation when the car pointer is _moving_, whilst the car is shown as _parked_.

By combining those two projections in _one subscription_, they could be both behind with updates, but the user experience will be much better as it will never be confusing. We could also say that those two projections belong to the same _projections group_.
{{% /alert %}}

Subscriptions need to maintain their own [checkpoints]({{< ref "checkpoint" >}}), so when the service that host a subscription restarts, it will start receiving events from the last known position in the stream.

Most often, you'd want to subscribe to the _global event stream_, so you can build read models, which compose information from different aggregates. Eventuous offers the [All stream subscription]({{< ref "all-stream-sub" >}}) for this use case. In some cases you'd need to subscribe to a regular stream using the [stream subscription]({{< ref "stream-sub" >}}).

In Eventuous, subscriptions are specific to event store implementation. We currently only provide subscriptions for EventStoreDB.

## The wrong way

One of the common mistakes people make when building an event-sourced application is to use an event store, which is not capable of handling realtime subscriptions. It forces developers to engage some sort of message bus to deliver new events to subscribers. There are [quite a few issues]({{< ref "the-right-way" >}}) with that approach, but the most obvious one is a two-phase commit.

{{% alert icon="📍" %}}
Read more about the **[Bad Bus →]({{< ref "the-right-way#event-bus" >}})**
{{% /alert %}}

When using two distinct pieces of infrastructure in one transaction, you risk one of those operations to fail. Let's use the following example code, which is very common:

```csharp
await _repository.Save(newEvents);
await _bus.Publish(newEvents);
```

If the second operation fails, the command side of the application would remain consistent. However, any read models, which projects those events, will not be updated. So, essentially, the reporting model will become inconsistent against the transactional model. The worst part is that the reporting model will never recover from the failure.

As mentioned, there are multiple issues of using a message bus as transport to deliver events to reporting models, but we won't be covering them on this page.

The easiest way to solve the issue is to use a database, which supports realtime subscriptions to event streams out of the box. That's why we use EventStoreDB as the primary event store implementation.
