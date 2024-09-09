---
title: "Apache Kafka"
description: "Producers and subscriptions for Kafka"
---

## Producer

Eventuous support producing messages to Apache Kafka topics. Currently, there is no schema registry support available when using Eventuous Kafka producer, and all messages are serialized using the default serializer as JSON, or using a custom serializer in any format. The serialized payload is then sent to Kafka topics as a byte array. There's no magic byte appended to the message, so you can use any serializer you want.

### Configuration

The producer configuration should be provided in the `KafkaProducerOptions` record. It only has one property: `ProducerConfig` which is a `ProducerConfig` class from the `Confluent.Kafka` library.

For example, you can configure and start the producer like this:

```csharp
var options = new KafkaProducerOptions(
    new ProducerConfig { BootstrapServers = "localhost:9092" }
);
await using var producer = new KafkaBasicProducer(options);
await producer.StartAsync(default);
await producer.Produce(new StreamName(topicName), events, new Metadata());
```

You can also add the producer to the dependency injection container:

```csharp
var options = new KafkaProducerOptions(
    new ProducerConfig { BootstrapServers = "localhost:9092" }
);
builder.Services.AddSingleton(options);
builder.Services.AddProducer<KafkaBasicProducer>();
```

Because Kafka producer requires lifecycle management, it also registers as a hosted service. When using `AddProducer` extension, the producer is registered as a hosted service automatically.

::: tip
The producer does not create topics in Kafka. You need to create topics manually before using the producer.
:::

### Producing messages

There are two ways to produce messages to Kafka: with or without partitioning.

Partition key is provided in `KafkaProduceOptions`, which is the optional parameter of the `Produce` method. If the partition key is not provided, the message is sent to a random partition.

```csharp
// Produce withouth partitioning
await producer.Produce(new StreamName(topicName), events, new Metadata());

// Produce with partitioning
await producer.Produce(new StreamName(topicName), events, new Metadata(), new("MyKey");
```

The `events` parameter is a list of `ProducedMessage` records. Each record contains the message payload, metadata and message id.

The producer supports publishing with immediate and delayed acknowledgement. The way to produce a message from the provided list is determined by presence of `ProducedMessage.OnAck` property. If the property is not set, the message is produced with immediate acknowledgement. If the property is set, the message is produced with delayed acknowledgement, and the `OnAck` function will be called when the producer receives an acknowledgement from Kafka. If the broker doesn't acknowledge the message, the producer will call the `OnNack` function if it is set in the `ProducedMessage` record.

For transmitting message details downstream, the producer sets two default message headers:

* `message-type`: the type of the message as string, usually coming from the type mapper
* `content-type`: the content type of the message, set to `application/json` when using the default serializer

Additional headers are set by the user in the `Metadata` record.

### Tracing

The Kafka producer creates a span for the whole batch of messages, so every message will get a span id and trace id headers set to the same value for all the messages.

When the batch only contains one message, the producer will also set the message type span tag.