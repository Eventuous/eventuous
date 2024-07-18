---
title: "RabbitMQ"
description: "Producers and subscriptions for RabbitMQ"
sidebar_position: 5
---

## Introduction

RabbitMQ is a popular message broker, and it can serve as a great integration infrastructure for communicating 
between services. Eventuous supports using RabbitMQ messaging with [producers](../../producers) for producing messages 
and [subscriptions](../../subscriptions) for consuming messages.

Eventuous producer for RabbitMQ publishes messages to _exchanges_. An exchanges can be considered similar to topics in other message brokers like Kafka or Google Pub/Sub, but, unlike topics, those messages are not persistent. Messages published to an exchange are distributed to queues that _bound_ (subscribed) to the exchange. When an exchange doesn't have any bindings, all the messages published to that exchange disappear.

For making RabbitMQ messaging work, you need to have exchanges and queues that bind to those exchanges. Eventuous creates an exchange by default if it doesn't exist when producing and consuming messages.

When using RabbitMQ for integration between services, the usual pattern is to have one exchange per service. Alternatively, one exchange can be used for messages about one logical topic like aggregate type or stream category.

## Producer

Eventuous RabbitMQ producer works the same way as any other message producer. It allows you to publish messages to RabbitMQ exchanges. Those messages can then be consumed by services that use Eventuous RabbitMQ subscriptions. You can also use other messaging libraries to consume messages as Eventuous doesn't manipulate sent messages in any way.

### Configuration

RabbitMQ producer's constructor requires the connection factory argument. When using the default ASP.NET Core dependency injection, you'd register the connection factory instance in the DI container as part of the bootstrap code. At the bare minimum, you need a RabbitMQ connection string to create a connection factory instance:

```csharp
builder.Services.AddSingleton(
    new ConnectionFactory {
        Uri = new Uri(rabbitMqConnectionString)
    }
); 
```

When that's done, you can register the producer by using `AddProducer` extension provided by Eventuous:

```csharp
builder.Services.AddProducer<RabbitMqProducer>();
```

As the producer will create an exchange when it publishes a message but the exchange doesn't exist, you can inform the producer how to configure those new exchanges. To do it, you can register or supply an instance of `RabbitMqExchangeOptions`. Available options are:

| Option       | Description                                         | Default value |
|--------------|-----------------------------------------------------|---------------|
| `Type`       | Exchange type                                       | `Fanout`      |
| `Durable`    | If the messages should be stored on disk            | `true`        |
| `AutoDelete` | If the exchange should disappear when it's not used | `false`       |

We recommend using the default exchange options, unless you want to use [routing keys][3] because fan-out exchanges don't support routing.

### Producing messages

The producer publishes messages to RabbitMQ exchanges. The `IProducer` signature uses the `StreamName` as a destination, the stream name value is used for the exchange name. You can see the stream name supplied to the producer as the exchange name.

When you tell the producer to publish a message to an exchange, it will check if the exchange exists. If the exchange doesn't exist, the producer will create one using the exchange options described above. This check only happens once per service lifetime, so it doesn't affect performance.

As the RabbitMQ producer implements the same `IProducer` interface as any other Eventuous producer, it has the same API as described on the [Producers](../../producers/implementation.md) page.

It's possible to tune the producer's behaviour when producing messages by supplying an optional produce options. For RabbitMQ, those options are represented by the `RabbitMqProduceOptions` record with the following properties:

| Option           | Description                                                                      |
|------------------|----------------------------------------------------------------------------------|
| `Exchange`       | Exchange name that the subscription binds to                                     |
| `FailureHandler` | Function to handle message processing errors, described [below](#error-handling) |
| `AppId`          | Application name                                                                 |
| `Expiration`     | Time-to-live for the message in milliseconds ([read more][2])                    |
| `Persisted`      | If the message should be persisted on disk, default is `true`                    |
| `Priority`       | Message priority, from 0 to 9                                                    |
| `RoutingKey`     | [Routing key][3] of the message, doesn't work with fan-out exchanges             |

When the produced message has metadata, all metadata values will be converted to message headers. Subscriptions will restore headers back to metadata. If message metadata contains a correlation id (`eventuous.correlation-id` key), the value will be added to the RabbitMQ message correlation id property.

## Subscriptions

Eventuous supports consuming messages from RabbitMQ using [subscriptions](../../subscriptions).

### Configuration

As any other subscription, it can be added to the Di container using `AddSubscription` extension:

```csharp
builder.Services.AddSubscription<RabbitMqSubscription, RabbitMqSubscriptionOptions>(
    "PaymentsIntegration",
    builder => builder
        .Configure(cfg => cfg.Exchange = "payments")
        .AddEventHandler<PaymentsHandler>()
);
```

The `Exchange` configuration property is mandatory as the subscription needs to know where it should consume messages from. Also, the subscription has a mandatory dependency on `ConnectionFactory`, so you'd need to register it in the container as described in the [producer configuration](#configuration) section above.

For consuming messages, the subscription needs a queue bound to the specified exchange. By default, the subscription id is used as the queue name. You can override the queue name by specifying an alternative value using `QueueOptions.Queue` property of the subscription options.

Besides the queue name, it's possible to configure the subscription with RabbitQ-specific parameters. Those include queue options, exchange options, and binding options. Eventuous provides default values for all those, so usually you would not need to change those. One option that likely should be overridden is the concurrency limit value, which is set to `1` by default. As RabbitMQ doesn't guarantee message ordering anyway, you can speed up message processing by increasing the concurrency limit, so the subscription can consume messages in parallel. Eventuous will also adjust the prefetch count to accommodate for increased number of consumers, if necessary.

As mention previously, RabbitMQ messages are published to an exchange, and consumed from a queue bound to that exchange. When the subscription starts, it makes sure that both the exchange and the queue exist, and the queue is bound to the exchange. If you start producing messages to an exchange created by the producer before starting the subscription at least once, and there's no queue and binding created upfront, those messages will be dropped. As long as the exchange has a binding to a subscription queue, the messages will be kept in the queue until consumed. Therefore, we recommend starting the subscription before producing messages.

RabbitMQ subscriptions can be configuring using the following options:

| Option             | Description                                                                                        |
|--------------------|----------------------------------------------------------------------------------------------------|
| `Exchange`         | Exchange name that the subscription binds to                                                       |
| `FailureHandler`   | Function to handle message processing errors, described [below](#error-handling)                   |
| `ExchangeOptions`  | Exchange options (see Producer configuration above)                                                |
| `QueueOptions`     | Subscription queue options (see below)                                                             |
| `BindingOptions`   | Options for the binding between the exchange and the queue (see below)                             |
| `ConcurrencyLimit` | The number of parallel consumers, default is one                                                   |
| `PrefetchCount`    | The number of [in-flight messages][1] per consumer, default is concurrency limit multiplied by two |

:::note Exchange configuration
Keep in mind that the exchange can be created by both the producer and the subscription, whatever starts first. If producer and subscription exchange options are different, the exchange won't be updated. The exchange options are only applied on exchange creation.
:::

:::note Queue and binding configuration
Both queue and binding options are only applied when those elements are created. Any update on queue and binding options in code that happen afterward won't trigger updating queues and bindings. You can still update those using RabbitMQ management API.
:::

For configuring the subscription queue, the following options are available in `RabbitMqQueueOptions`:

| Name         | Description                                                                       |
|--------------|-----------------------------------------------------------------------------------|
| `Queue`      | Overriders the default queue name, which is set to subscription id by default     |
| `AutoDelete` | Default is `false`, so the queue will survive restarts                            |
| `Exclusive`  | Default is `false`, change it if you want to only have a single consumer instance |
| `Durable`    | Default is `true`, so the queue will be persisted on disk                         |

Finally, you can configure exchange to queue binding using `RabbitMqBindingOptions`:

| Name         | Description                                                                       |
|--------------|-----------------------------------------------------------------------------------|
| `RoutingKey` | Optional [routing key][3] for the binding, it doesn't work with fan-out exchanges |

If you set the routing key for the binding but the exchange is configured as fan-out, Eventuous will produce a warning but will create the binding. Routing key specified when producing messages will be ignored for fan-out exchanges.

### Error handling

Eventuous subscriptions don't throw exceptions when message handler fails. This behaviour can be changed by changing the `ThrowOnError` subscription option. For RabbitMQ subscriptions, this behaviour is extended by using the requeue feature of the broker. Therefore, by default if the message handler throws an exception, the message will be put back in the queue and consumed again later. This helps to deal with transient failures in the message handler, but also severely impacts message processing order as the retried message is put at the end of the queue.

If you want to ensure that messages are consumed in relative order, you'd need to make sure that message processing is retried inside the handler. Still, if the error doesn't get resolved by retries, the consumer will eventually time out and the message will be put back in the queue. If the message is causing the consumer to fail unconditionally, it is a _poison message_, and it can take put the system into an endless retry loop.

The requeue behaviour is provided by the default failure handler. It's possible to override it by setting the `FailureHandler` property of the subscription options.

## Other messaging frameworks

It's important to understand how Eventuous messaging for RabbitMQ is different compared to other messaging middleware libraries for .NET like MassTransit or NServiceBus.

The most apparent difference is that both MassTransit and NServiceBus use _message type-based routing_. It means that by default they create exchanges for each message type produced by the application. Another default is that the .NET class name is used to compute the exchange name. As a result, any refactoring of the message schema code like renaming classes, changing namespaces, etc., causes disruption for downstream consumers. As fully-qualified class names are also used for deserialization, changes in message class names as well as their namespaces causes issue for downstream consumers and requires coordinated deployments.

Eventuous uses a different approach where the service normally produces messages to its own single exchange which is named after the service. Each subscription gets its own queue, and creates a binding to a particular exchange. So, each consumer will get messages with different types, from a particular service. Message type is supplied as a default RabbitMQ `Type` message property. It uses Eventuous [type map](../../persistence/serialisation.md#type-map) for mapping CLR types to strings, so refactoring namespaces and class name will not affect message routing. In addition, messages are delivered in relative order from one service to another. Certainly, it might affect the system performance as if the subscription queue gets congested, it will keep growing. That can be mitigated by using message priority, which is supported by RabbitMQ.

[1]: https://www.rabbitmq.com/docs/confirms#channel-qos-prefetch
[2]: https://www.rabbitmq.com/docs/ttl#per-message-ttl-in-publishers
[3]: https://www.rabbitmq.com/tutorials/tutorial-four-dotnet