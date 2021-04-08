---
title: "Checkpoints"
description: "What's a checkpoint and why you need to store it"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "subscriptions"
weight: 520
toc: true
---

When you subscribe to an event store, you need to decide what events you want to receive. A proper event store would allow you to subscribe to any event stream, or to a global stream (_All stream_), which contains all the events from the store, ordered by the time they were appended. Event-oriented brokers that support persistence as ordered event logs also support subscriptions, normally called _consumers_, as it's the publish-subscribe broker terminology.

The subscription decides at what stream position it wants to start receiving events. If you want to process all the historical events, you'd subscribe from the beginning of the stream. If you want to only receive real-time events, you need to subscribe from _now_.

## What's the checkpoint?

As the subscription receives and processes events, it moves further along the stream it subscribed to. Every event the subscription receives and processes has a position in the subscribed stream. This position can be used as a _checkpoint_ of the subscription. If the application that hosts the subscription eventually shuts down, you'd want the subscription to resubscribe from the position, which was processed last, plus one. That's how you achieve _exactly one_ event handling. Therefore, the subscription needs to take care about storing its checkpoint somewhere, so the last known position can be retrieved from the checkpoint store and used to resubscribe.

Some log-based brokers also use the term _offset_ to describe the checkpoint concept.

## Checkpoint store

Eventuous provides an abstraction, which allows subscriptions to store checkpoints reliably. You can decide to store it in a file or in a database. You can also decide if you want to store a checkpoint after processing each event, or only flush it now and then. Periodical checkpoint flush decreases the pressure on the infrastructure behind the checkpoint store, but also requires you to make your subscription idempotent. It's usually hard or impossible for integration since you can rarely check if you published an event to a broker or not. However, it can work for read model projections.

{{% alert icon="😱" %}}
**Keep the checkpoint safe.** When the checkpoint is lost, the subscription will get all the events. It might be intentional when you are creating a brand new [read model]({{< ref "rm-concept" >}}), then it's okay. Otherwise, you get undesired consequences.
{{% /alert %}}

The checkpoint store interface is simple, it only has two functions:

```csharp
interface ICheckpointStore {
    ValueTask<Checkpoint> GetLastCheckpoint(
        string checkpointId,
        CancellationToken cancellationToken
    );

    ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint checkpoint,
        CancellationToken cancellationToken
    );
}
```

The `Checkpoint` record is a simple record, which aims to represent a stream position in any kind of event store:

```csharp
record Checkpoint(string Id, ulong? Position);
```

Out of the box, Eventuous provides a checkpoint store for MongoDB.
