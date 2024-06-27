---
title: "Implementation"
weight: 100
description: "How producers are implemented"
---

## Abstraction

Eventuous has two types of producers: with or without produce options. The producer without produce options is a simple producer that does not support any options, and it normally produces messages as-is. The producer with produce options can have more fine-grained control on how messages are produced. Produce options are provided per message batch.

The base interface for a producer is called `IEventProducer<TProduceOptions>` and its only method is `Produce`.

```csharp
Task Produce(
    StreamName                   stream,
    IEnumerable<ProducedMessage> messages,
    TProduceOptions?             options,
    CancellationToken            cancellationToken = default
);
```

The `IEventProducer` interface has a property named `ReadyNow`, which indicates the readiness of the producer. This property is important for gateways to determine if the producer is prepared to produce messages. In certain scenarios, the producer may need to set up or verify the necessary infrastructure such as queues or topics before it can begin producing messages. Once this setup is complete, the producer should set the `ReadyNow` property to `true`.

## Base producer

There are two abstract base classes for producers, one without options, and the other one with options.

The purpose for the base class is to enable tracing for produced messages. All producers implemented in Eventuous use the base producer class. For the purpose of tracing, the base producer class accepts `ProducerTracingOptions` as a parameter.

```csharp
public record ProducerTracingOptions {
    public string? MessagingSystem  { get; init; }
    public string? DestinationKind  { get; init; }
    public string? ProduceOperation { get; init; }
}
```

These options are used to set the producer trace tags that are specific for the infrastructure. For example, the messaging system tag for `RabbitMqProducer` is `rabbitmq`.

Both base classes implement the `Produce` method. It is only used to enable tracing. The actual producing is done by the `ProduceMessages` abstract method. When implementing a new producer using the base class, you'd only need to implement the `ProduceMessages` method.

You can see that for producing a message, the producer gets a collection of `ProducedMessage` record. It looks like this:

```csharp
public record ProducedMessage {
    public object               Message     { get; }
    public Metadata?            Metadata    { get; init; }
    public Guid                 MessageId   { get; }
    public string               MessageType { get; }
    public AcknowledgeProduce?  OnAck       { get; init; }
    public ReportFailedProduce? OnNack      { get; init; }
}
```

The `Message` property represents the actual message payload. Producers typically use an `IEventSerializer` instance to serialize the message payload. However, in certain situations, producers may need to comply with their supporting infrastructure and use a different method for serializing the message payload. In such cases, the `MessageType property can be included in the produced message body or header, allowing for proper deserialization by subscribers.

## Registration

Eventuous provides several extensions to the `IServiceCollection` interface to register producers. You can provide a pre-made producer instance, a function to resolve the producer from the `IServiceProvider`, or simply the producer type if its dependencies can be resolved automatically.

For instance, if you have already registered the `EventStoreClient` instance, you can register the `EventStoreProducer` as follows:

```csharp title="Program.cs"
builder.Services.AddEventProducer<EventStoreProducer>();
```

If a producer requires some work to be done before it is ready, it should implement the `IHostedService` interface, allowing it to perform necessary startup tasks in its `StartAsync` method. When using any of the `AddEventProducer` extensions, if the producer implements `IHostedService`, it will be registered as such.

Keep in mind that producers are typically registered as singletons. If you require multiple producer instances for the same infrastructure within your application (like two RabbitMQ producers for different RabbitMQ instances), you must provide them as direct dependencies rather than registering them. It's uncommon to need multiple producer instances, unless you are utilizing gateways. The various `AddEventProducer` overloads register the specified producer as both `IEventProducer` and its implementing service class. For example, `AddEventProducer<RabbitMqProducer>` will register both `IEventProducer` and `RabbitMqProducer`. Gateway registration extensions are capable of utilizing individual producer instances as dependencies.