---
title: "EventStoreDB"
description: "EventStoreDB is a database for event-sourced applications"
sidebar_position: 1
---

Eventuous uses [EventStoreDB][1] as the default event store. It's a great product, and we're happy to provide first-class support for it. It's also a great product for learning about Event Sourcing and CQRS.

Below, you can find Eventuous components that are implemented for EventStoreDB. 

:::tip
Remember to check [Event Store Cloud](https://www.eventstore.com/event-store-cloud).
:::

## Events persistence

The `EsdbEventStore` is an implementation of `IEventStore` interface. It uses the EventStoreDB gRPC client, so the legacy TCP protocol isn't supported. Therefore, Eventuous only works with EventStoreDB 20+, which has gRPC support.

The easiest way to use it is to register it in the DI container. As `EsdbEventStore` needs an EventStoreDB client as a dependency, you'd need to register the client first. The client package has DI registration extensions that allow you to register the client using a single line of code:

```csharp
services.AddEventStoreClient(connectionString);
```

The connection string usually comes from the application configuration. When running locally using Docker, you might use a connection string like:

```csharp
var connectionString = "esdb://localhost:2113?tls=false";
```

When running in production, you'd use a secure connection string, which contains a username and password. You can find more information about connection strings in the [EventStoreDB documentation][3].

Further, you need to tell Eventuous to use the `EsdbEventStore` for its aggregate store. We have a simple extension that allows you to do that:

```csharp
services.AddAggregateStore<EsdbEventStore>();
```

When that's done, Eventuous would persist aggregates using EventStoreDB when you use the [command service](../../application/app-service).

## Subscriptions

EventStoreDB supports multiple [subscription](../../subscriptions/subs-concept) types, and all of them are supported by Eventuous. The main choice you'd need to make is to use [catch-up][4] or [persistent subscription][5].

### All stream subscription

Subscribing to all events in the store is extremely valuable. This way, you can build comprehensive read models, which consolidate information from multiple aggregates. You can also use such a subscription for integration purposes, to convert and publish integration events.

:::note
[Read more](https://zimarev.com/blog/event-sourcing/all-stream/) about benefits of using the global event stream.
:::

For registering a subscription to `$all` stream, use `AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions> as shown below:

```csharp
builder.Services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>()
);
```

Subscription options for `AllStreamSubscription` are defined in `AllStreamSubscriptionOptions` class.

| Option               | Description                                                                                                                                                                                                                                                        |
|:---------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `SubscriptionId`     | Unique subscription identifier.                                                                                                                                                                                                                                    |
| `ThrowOnError`       | If `true`, an exception will be thrown if the subscription fails, otherwise the subscription continues to run. Default is `false`.                                                                                                                                 |
| `EventSerilizer`     | Serializer for events, if `null` the default serializer will be used.                                                                                                                                                                                              |                                                                                                                                                                  
| `MetadataSerilizer`  | Serializer for metadata, if `null` the default serializer will be used.                                                                                                                                                                                            |                                                                                
| `Credentials`        | EventStoreDB user credentials. If not specified, the credentials specified in the `EventStoreClientSettings` will be used.                                                                                                                                         |                                                                              
| `ResolveLinkTos`     | If `true`, the subscription will automatically resolve the event link to the event that caused the event. Default is `false`.                                                                                                                                      |
| `ConcurrencyLimit`   | Maximum number of events to be processed in parallel. Default is `1`.                                                                                                                                                                                              |                                                                                             
| `EventFilter`        | Filter for events, if `null`, the subscription will filter out system events.                                                                                                                                                                                      |                                                                                                                                                                         
| `CheckpointInterval` | Interval between checkpoints when event filter is used. Default is `10` events. This interval tells the subscription to report the current checkpoint when the subscription doesn't receive any events for this interval because all the events were filtered out. |

#### Checkpoint store

`AllStreamSubscription` is a catch-up subscription that is fully managed on the client side (your application), so you need to manage the [checkpoint](../../subscriptions/checkpoint). You can register the checkpoint store using `AddCheckpointStore<T>`, but in that case it will be used for all subscriptions in the application. It might be that your app has multiple subscriptions, and you want to use different checkpoint stores for each of them. In that case, you can register the checkpoint store for each subscription using `UseCheckpointStore<T>` extension of the subscription builder

```csharp
builder.Services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .UseCheckpointStore<MongoCheckpointStore>()
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>()
);
```

#### Concurrent event handlers

As any catch-up subscription, subscription to `$all` runs sequentially, processing events one by one. In many cases that's enough, but sometimes you might want to speed it up, and allow parallel processing of events. To do that, you need to set the `ConcurrencyLimit` subscription option property to a value that is equal to the number of events being processed in parallel. In addition, you need to tell the subscription how to distribute events into partitions. That is needed as you rarely can tolerate processing events in a completely random order, so you can partition events using some key, and distribute them to different partitions.

Here is an example of using `AllStreamSubscription` with `ConcurrencyLimit` and partitioning by stream name:

```csharp
var partitionCount = 2;
builder.Services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .Configure(cfg => cfg.ConcurrencyLimit = partitionCount)
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>()
        .WithPartitioningByStream(partitionCount)
);
```

You can build your own partitioning strategy by implementing the `GetPartitionKey` function:

```csharp
public delegate string GetPartitionKey(IMessageConsumeContext context);
```

and then using it in the `WithPartitioning` extension:

```csharp
builder => builder
    .Configure(cfg => cfg.ConcurrencyLimit = partitionCount)
    ... // add handlers
    .WithPartitioning(partitionCount, MyPartitionFunction)
```

### Single stream subscription

Although subscribing to `$all` using [`AllStreamSubscription`](#all-stream-subscription) is the most efficient way to create, for example, [read models](../../read-models) using all events in the event store, it is also possible to subscribe to a single stream.

For example, you can subscribe to the `$ce-Booking` stream to project all events for all the aggregates of type `Booking`, and create some representation of the state of the aggregate in a queryable store.

Another scenario is to subscribe to an integration stream, when you use EventStoreDB as a backend for a messaging system.

For that purpose you can use the `StreamSubscription` class.

For registering a subscription to a single stream, use `AddSubscription<StreamSubscription, StreamSubscriptionOptions> as shown below:

```csharp
builder.Services.AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
    "BookingsStateProjections",
    builder => builder
        .Configure(cfg => {
            cfg.StreamName = "$ce-Booking";
            cfg.ResolveLinkTos = true;
        )
        .AddEventHandler<BookingStateProjection>()
);
```

Subscription options for `StreamSubscription` are defined in `StreamSubscriptionOptions` class.

| Option               | Description                                                                                                                        |
|:---------------------|:-----------------------------------------------------------------------------------------------------------------------------------|
| `SubscriptionId`     | Unique subscription identifier.                                                                                                    |
| `StreamName`         | Name of the stream to subscribe to.                                                                                                |
| `ThrowOnError`       | If `true`, an exception will be thrown if the subscription fails, otherwise the subscription continues to run. Default is `false`. |
| `EventSerilizer`     | Serializer for events, if `null` the default serializer will be used.                                                              |
| `MetadataSerilizer`  | Serializer for metadata, if `null` the default serializer will be used.                                                            |
| `Credentials`        | EventStoreDB user credentials. If not specified, the credentials specified in the `EventStoreClientSettings` will be used.         |
| `ResolveLinkTos`     | If `true`, the subscription will automatically resolve the event link to the event that caused the event. Default is `false`.      |
| `IgnoreSystemEvents` | Set to true to ignore system events. Default is `true`.                                                                            |
| `ConcurrencyLimit`   | Maximum number of events to be processed in parallel. Default is `1`.                                                              |

:::info
At the bare minimum, you must define the stream name in the subscription options.
:::

:::caution Link events
When subscribing to a stream that contains link events (for example, `$ce-` category stream), you should set the `ResolveLinkTos` option to `true` to resolve the link to the original event that is linked to the link event.
:::

#### Checkpoint store

`StreamSubscription` is a catch-up subscription that is fully managed on the client side (your application), so you need to manage the [checkpoint](../../subscriptions/checkpoint). The checkpoint store configuration for stream subscriptions is identical to the one for the [`AllStreamSubscription`](#checkpoint-store).

#### Concurrent event handlers

The single stream subscription is identical to the `$all` stream subscription in terms of the event handlers execution. By default, all the events are processed one-by-one, but you can use the `ConcurrencyLimit` option to process multiple events in parallel.

You can use the stream name partitioner when subscribing to a category (`$ce`) stream. In that case events for a single aggregate instance will always be processed sequentially, but events for different aggregate instances can be processed in parallel.

Read more about concurrent event processing on the [all stream subscription](#concurrent-event-handlers) page.

### Persistent subscriptions

:::caution Ordered events
EventStoreDB persistent subscriptions do not guarantee ordered event processing. Therefore, we only recommend using them for integration purposes (reactions).
:::

:::note
Persistent subscription to $all stream is only supported from EventStoreDB version 21.10.0.
:::

Unlike catch-up subscriptions, persistent subscriptions are fully managed by the database server. It is also possible to have multiple consumers for the same subscription, and the events will be distributed between them. The server also manages retries when a consumer fails to acknowledge the event. Because of the retries, batched delivery, and multiple consumers, persistent subscriptions don't guarantee ordered event processing.

Read more about persistent subscriptions in the [EventStoreDB documentation](https://developers.eventstore.com/server/v21.10/persistent-subscriptions.html).

There are some operations that must be completed before a persistent subscription starts working, In particular, the consumer group must be created on the server before a consumer can start consuming events. Eventuous implicitly creates a consumer group if necessary. The consumer group name is the same as the subscription id.

Registering a persistent subscription is very similar to registering a catch-up subscription. The only difference is that you need to use one of the `PersistentSubscription` classes instead of the `StreamSubscription` or `AllStreamSubscription` class.

Here's how you set up a persistent subscription to a single stream:

```csharp
builder.Services.AddSubscription<StreamPersistentSubscription, StreamPersistentSubscriptionOptions>(
    "PaymentIntegration",
    builder => builder
        .Configure(x => x.StreamName = PaymentsIntegrationHandler.Stream)
        .AddEventHandler<PaymentsIntegrationHandler>()
);
```

When setting up a persistent subscription to the `$all` stream, you don't need to specify the stream name, and you need to use the `AllPersistentSubscription` class:

```csharp
builder.Services.AddSubscription<AllPersistentSubscription, AllPersistentSubscriptionOptions>(
    "CrossAggregateIntegration",
    builder => builder.AddEventHandler<CrossAggregateIntegrationHandler>()
);
```

There's no need to use a checkpoint store as persistent subscription checkpoint is maintained by the server.

## Producer

In a prototype or small-scale production application, you can use EventStoreDB as a message broker. In that case, you can use the `EventStoreProducer` to publish events to the database. Unlike the aggregate store, [producers](../../producers) allow publishing events that aren't necessarily domain events.

You can then register the `EventStoreProducer` in the DI container. As the producer needs the `EventStoreClient` or `EventStoreClientSettings` as a dependency, you need to register those as well.

```csharp
builder.Services.AddEventStoreClient("esdb://localhost:2113?tls=false");
builder.Services.AddEventProducer<EventStoreProducer>();
```

To produce an event, the producer needs a stream name, a message, and (optionally) the message metadata:

```csharp
[EventType("TestMessage")]
public record TestMessage(string Text);

var message = new TestMessage("Hello world!");
await producer.Produce("test-stream", message, new Metadata());
```

You can also produce multiple messages at once, but then you need to wrap each message to a `ProducedMessage` object:

```csharp
var messages = events.Select(x => new ProducedMessage(x, new Metadata()));
await producer.Produce("test-stream", messages);
```

[1]: https://eventstore.com
[2]: https://www.eventstore.com/event-store-cloud
[3]: https://developers.eventstore.com/clients/grpc/
[4]: https://developers.eventstore.com/clients/grpc/subscriptions.html
[5]: https://developers.eventstore.com/clients/grpc/persistent-subscriptions.html
