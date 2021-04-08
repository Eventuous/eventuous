---
title: "The Right Way"
description: "Event Sourcing done right"
date: 2020-10-06T08:48:57+00:00
lastmod: 2020-10-06T08:48:57+00:00
draft: false
images: []
menu:
  docs:
    parent: "prologue"
weight: 110
toc: true
---

If you ever searched for a diagram that can give you an idea of what Event Sourcing is about, you can find many images out there. The only issue is that most of them are confusing or just plain wrong.

It's a bold statement, you'd say, isn't it? It might be, yes. Yet, a lot of people who claim that Event Sourcing is hard, have either never tried it or made mistakes when doing the implementation.

Eventuous does not claim to be the Almighty Source of Truth. Quite the opposite, we argue that the library's code is opinionated and leans towards what was done before with this library in production with success.

Still, as people demand answers about _how to do it right_, we have one for you here.

![The right way](/images/the-right-way.png)

Quite a few diagrams from articles that claim to explain to you about what Event Sourcing is and how to implement it suffer from the same issues:

- Using some kind of bus to propagate [domain events]({{< ref "events" >}}) to read models
- Bring unnecessary components and, therefore, complexity to the picture
- Mixing up Event-Driven Architecture (EDA) with Event Sourcing
- Not using domain events as the domain model state

Let's briefly go through those issues.

### Event bus

Message brokers are great, really. It's way better to integrate (micro)services using asynchronous messaging rather than RPC calls. RPC calls introduce spatial and temporal coupling, something you'd want to avoid when doing services.

Still, the integration concern is orthogonal to Event Sourcing; as much as domain events _enable_ message-based integration, it's not a requirement.

When propagating events to reporting models (read models, query side, whatever you call it) using a broker, you will encounter the following issues:

- **Our of order events.** When projecting events to reporting models, you really want the events to be processed in the same order as they were produced. Not all message brokers give you such a guarantee, as they were designed for a different purpose.
- **Two-phase commit.** We already mentioned this issue in this documentation. Once again, a database used to store the events and the message broker are two distinct infrastructure components. You can rarely to make a single operation of persisting the event and publishing it to the broker transactional. As a result, one of these operations could fail, effectively making a part of your system inconsistent (having an invalid state). Claims that you can apply technical patterns like _Outbox_ to mitigate the issue are valid. However, the Outbox pattern implementation is very complex and often exceeds the complexity of the essential part of the system itself.
- **Replay.** This one is probably the most important. Message brokers are often used for the purpose of integration. It means that you find event consumers out there (which you cannot control) reacting to published events. Those consumers produce side effects. Unlike reporting models, integration side effects are not idempotent, as they don't expect an event to happen multiple times. Effectively, when you want to replay all the events from your event store to rebuilt a single reporting model, you will affect all the other consumers. Not only those will be your own reporting models, which you probably don't want to rebuild. You'll also send historical events to consumers outside your area of control, which will start reacting to those events and, more often than not, produce undesired side effects.

{{% alert icon="👉" %}}
Do not use message brokers to publish events when handling commands (do use them to receive commands, though). Make sure your event store database is able to support real-time subscriptions, so you can subscribe to new events, which are already persisted, and do whatever you want with them _after_ they are persisted.
{{% /alert %}}

Publishing to a message broker from a [subscription]({{< ref "subs-concept" >}}) is an entirely legit scenario. Do not build reporting models by consuming from a broker unless it's an event log-based broker like Apache Kafka, Apache Pulsar or Azure Event Hub. Prefer projecting events by subscribing directly to your event store.

### Using reporting database as system state

The very nature of Event Sourcing as a persistence mechanism makes it fully consistent. You append an event to the aggregate stream, using the aggregate version as the expected version for the optimistic concurrency as a transaction. You restore the aggregate state by reading those events. It is atomic, consistent, isolated and durable - ACID, read your own writes and all that.

Still, you find lots of articles describing that just storing events to some kind of ordered log is Event Sourcing, whilst they don't use those events as the source of truth. Instead, they project events to another store asynchronously, outside the transaction scope, and claim it to be Event Sourcing. Make no mistake, it is not. Unlike true Event Sourcing, this method does not guarantee consistency. Using some database on a side, which is fed by events out-of-proc and outside the transaction boundary, inevitably produces a delay between the moment an event is stored, and the moment the state change happens in another database. That other database effectively is a reporting model, but it cannot be considered as consistent storage for the system state. In a genuinely event-sourced system, you can always get the state of any object, which is fully consistent, from the event store. From a projected store, you get a particular state projection, which is not necessary up to date. It is fine for reporting models, but it is not fine for decision-making.

{{% alert icon="👉" color="warning" %}}
Handling a command produces a state transition of one domain object, and, therefore, the whole system. You must ensure you operate with consistent system state when making a decision to execute the command (or not).
{{% /alert %}}

### Event-Driven vs Event Sourcing

What we described in two previous paragraphs are two parts of Event-Driven Architecture (EDA). Although Event Sourcing enables you to implement EDA, those two concepts are orthogonal. You can implement an event-sourced system, which doesn't distribute events to the outside. You can also implement an event-driven system without using Event Sourcing.

{{< alert icon="👉" color="info" >}}
Event Sourcing is a way to persist state. Event-Driven Architecture is about integrating system components using events distributed via a message broker.
{{< /alert >}}
