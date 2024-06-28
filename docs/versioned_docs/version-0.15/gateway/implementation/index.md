---
title: "Implementation"
description: "Implementing the Gateway"
weight: 10
---

Gateway is a ready-made Eventuous construct that needs other components to work properly (subscription and producer at least). Any of subscription and producer type provided by Eventuous, as well as custom ones, can be used in a gateway.

When you implement a gateway, the only things that you need to do are:
* Provide an optional transformation and filtering function
* Register a gateway given one subscription and one producer

## Transformation

One common scenario for a gateway is to distribute domain events to other systems via a message broker. However, it's not a good idea to publish domain events as-is for others to consume. By doing so, you are coupling downstream consumers to your domain model. When you decide to change your domain model, and, possibly, enrich your domain events, you force developers of downstream consumers to change their code. Effectively, you either lose the ability to change your domain model, or you are coupling downstream systems to your bounded context.

That's why we strongly suggest to establish a contract-like event schema for the outside world, and keep it stable. That's also one more reason not to allow other systems to subscribe to your domain (_private_) events directly from the event store, but deploy a gateway and distribute your 
integration and notification (_public_) events using a message broker.

If you decide to revamp the public events schema, you can do it the same way as you'd publish a new API schema version. Using a gateway, you can produce multiple public events given one private event, so you can always produce different versions of the same event as a public event for the period of support for both versions. In short, using a clear private vs public event transformation you can treat integrations events schema as a versioned contract, and be free to evolve your domain (private) events as you wish.

Based on the producer kind (with or without options), you can perform the transformation using a function that implements `RouteAndTransform` or `RouteAndTransform<TProducerOptions>`:

```csharp
delegate ValueTask<GatewayMessage[]> RouteAndTransform(IMessageConsumeContext context);
delegate ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform<TProduceOptions>(
    IMessageConsumeContext message
);
```

If you prefer to do the transformation using classes, you can implement the `IGatewayTransform` interface:

```csharp
interface IGatewayTransform {
    ValueTask<GatewayMessage[]> RouteAndTransform(IMessageConsumeContext context);
}

interface IGatewayTransform<TProduceOptions> {
    ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform(
        IMessageConsumeContext context
    );
}
```

As you can see, both ways require to return an array of `GatewayMessage` objects. The returned array could be empty if you don't want to produce a public event for a given private event.

The `GatewayMessage` signatures are:

```csharp
record GatewayMessage(StreamName TargetStream, object Message, Metadata? Metadata);

record GatewayMessage<TProduceOptions>(
    StreamName      TargetStream,
    object          Message,
    Metadata?       Metadata,
    TProduceOptions ProduceOptions
) : GatewayMessage(TargetStream, Message, Metadata);
```

## Registration

There's no other component to implement for getting a working gateway. You need to register a gateway using one subscription, one producer, and one transformation function or class.

To register a gateway, use one of the `AddGateway` methods. For example, the sample application uses this gateway registration for publishing integration events to EventStoreDB integration stream:

```csharp
services
    .AddGateway<AllStreamSubscription, AllStreamSubscriptionOptions, EventStoreProducer>(
        "IntegrationSubscription",
        PaymentsGateway.Transform
    );
```

Here, `PaymentsGateway.Transform` is a transformation function that transforms private events to public events.

You can use any available subscription or producer for the gateway. If the subscription needs a checkpoint store, you can either register it globally, or provide one using the subscription configuration function for the `AddGateway` method. The same function can be used for any additional subscription configuration, like partitioning.

There are overloads to register a gateway using a producer with options as well. You can also provide additional functions to configure the subscription when using, for example, a specific checkpoint store. You can also register a gateway that uses the transformation class instead of a function.

All of the `AddGateway` overloads also have a parameter called `awaitProduce` of type `bool` that is set to `true` by default. It only works for producers that support delayed delivery reports, like the Kafka producer. When you set it to `false`, you might get better performance of the producer, but you can get undesired consequences if the producer fails for some messages, as when the produce action is retried you might get duplicate messages produced.
