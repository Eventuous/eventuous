---
title: "Serialisation"
description: "Serialisation"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "persistence"
weight: 330
toc: true
---

As described on the [Domain events]({{< ref "events" >}}) page, events must be (de)serializable. Eventuous doesn't care about the serialisation format, but requires you to provide a serializer instance, which implements the `IEventSerializer` interface.

The serializer interface is very simple:

```csharp
interface IEventSerializer {
    object? Deserialize(ReadOnlySpan<byte> data, string eventType);

    byte[] Serialize(object evt);
}
```

### Type map

For deserialization, the serializer will get the binary payload and the event type as string. Event store is unaware of your event types, it just stores the payload in a binary format to the database, along with the event type as string. It is up to you how your strong event types map to the event type string.

{{< alert icon="✋" color="warning" >}}
We do not advise using fully-qualified type names as event types. It will block your ability to refactor the domain model code.
{{< /alert >}}

Therefore, we need to have a way to map strong types of the events to strings, which are used to identify those types in the database and for serialisation. For that purpose, Eventuous uses the `TypeMap`. It is a singleton, which is available globally. When you add new events to your domain model, remember to also add a mapping for those events. The mapping is static, so you can implement it anywhere in the application. The only requirement is that the mapping code must execute when the application starts.

For example, if you have a place where domain events are defined, you can put the mapping code there, as a static member:

```csharp
static class BookingEvents {
    // events are defined here

    public static void MapBookingEvents() {
        TypeMap.AddType<RoomBooked>("RoomBooked");
        TypeMap.AddType<BookingPaid>("BookingPaid");
        TypeMap.AddType<BookingCancelled>("BookingCancelled");
        TypeMap.AddType<BookingImported>("BookingImported");
    }
}
```

Then, you can call this code in your `Startup`:

```csharp
BookingEvents.MapBookingEvents();
```

### Default serializer

Eventuous provides a default serializer implementation, which uses `System.Text.Json`. You just need to register it in the `Startup` to make it available for the infrastructure components, like [aggregate store]({{< ref "aggregate-store" >}}) and [subscriptions]({{< ref "subs-concept" >}}).

```csharp
services.AddSingleton<IEventSerializer>(
    new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
    )
);
```
