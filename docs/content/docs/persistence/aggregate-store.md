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

{{% alert icon="👉" %}}
Eventuous does not have a concept of Repository. Find out why [on this page]({{< ref "../faq/persistence" >}}).
{{% /alert %}}

Eventuous provides a single abstraction for the domain objects persistence, which is the `AggregateStore`.

The `AggregateStore` uses the `IEventStore` abstraction to be persistence-agnostic, so it can be used as-is, when you give it a proper implementation of event store.

We have only two operations in the `AggegateStore`:
- `Load` - retrieves events from an aggregate stream and restores the aggregate state using those events.
- `Store` - collects new events from an aggregate and stores those events to the aggregate stream.

The `AggregateStore` constructor needs two arguments:
- [Event store]({{< ref "event-store" >}}) (`IEventStore`)
- [Event serializer]({{< ref "serialisation" >}}) (`IEventSerializer`)

Our [`ApplicationService`]({{< ref "app-service" >}}) uses the `AggregateStore` in its command-handling flow.

## Infrastructure

Eventuous supports [EventStoreDB](https://eventstore.com) out of the box, but only v20+ with gRPC protocol.

Using this pre-made event persistence is easy. You can register the necessary dependencies in your `Startup` class when using ASP.NET Core:

```csharp
services.AddSingleton(new EventStoreClient(
    EventStoreClientSettings.Create(connectionString)
));
services.AddSingleton<IEventSerializer>(
    new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
    )
);
services.AddSingleton<IEventStore, EsDbEventStore>();
services.AddSingleton<IAggregateStore, AggregateStore>();
```

{{% alert icon="👉" %}}
Make sure to read about [events serialisation]({{< ref "serialisation">}}).
{{% /alert %}}
