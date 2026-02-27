# Eventuous Azure Service Bus Integration

NuGet package: `Eventuous.Azure.ServiceBus`
Namespace: `Eventuous.Azure.ServiceBus.Producers`, `Eventuous.Azure.ServiceBus.Subscriptions`
Source: `src/Azure/src/Eventuous.Azure.ServiceBus/`

## Producer

`ServiceBusProducer` extends `BaseProducer<ServiceBusProduceOptions>` and implements `IHostedProducer`, `IAsyncDisposable`.

### Constructor

```csharp
public ServiceBusProducer(
    ServiceBusClient             client,      // Azure SDK ServiceBusClient
    ServiceBusProducerOptions    options,
    IEventSerializer?            serializer = null,
    ILogger<ServiceBusProducer>? log        = null
)
```

The producer creates a `ServiceBusSender` from the client for the configured queue/topic.

### ServiceBusProducerOptions

```csharp
public class ServiceBusProducerOptions {
    public required string QueueOrTopicName { get; init; }
    public ServiceBusSenderOptions? SenderOptions { get; init; }
    public ServiceBusMessageAttributeNames AttributeNames { get; init; } = new();
}
```

### ServiceBusProduceOptions (per-message)

```csharp
public class ServiceBusProduceOptions {
    public string?  Subject          { get; set; }
    public string?  To               { get; set; }
    public string?  ReplyTo          { get; set; }
    public string?  SessionId        { get; init; }       // for session-enabled entities
    public string?  ReplyToSessionId { get; init; }
    public TimeSpan TimeToLive       { get; set; } = TimeSpan.MaxValue;
}
```

### How it works

- Single messages sent via `SendMessageAsync`; multiple messages batched automatically via `ServiceBusMessageBatch`
- Event type stored in `ApplicationProperties["MessageType"]`; content type in `ServiceBusMessage.ContentType`
- Metadata and additional headers added as application properties (filtered by Service Bus-compatible types)
- Session ID on produce options enables ordered message processing
- Supports delivery acknowledgement callbacks

### DI Registration

```csharp
services.AddSingleton(new ServiceBusClient("your-connection-string"));
services.AddProducer<ServiceBusProducer>(sp =>
    new ServiceBusProducer(
        sp.GetRequiredService<ServiceBusClient>(),
        new ServiceBusProducerOptions { QueueOrTopicName = "my-topic" }
    )
);
```

## Subscription

`ServiceBusSubscription` extends `EventSubscription<ServiceBusSubscriptionOptions>`.

### Constructor

```csharp
public ServiceBusSubscription(
    ServiceBusClient              client,
    ServiceBusSubscriptionOptions options,
    ConsumePipe                   consumePipe,
    ILoggerFactory?               loggerFactory,
    IEventSerializer?             eventSerializer
)
```

### ServiceBusSubscriptionOptions

```csharp
public record ServiceBusSubscriptionOptions : SubscriptionOptions {
    public required IQueueOrTopic QueueOrTopic { get; set; }
    public ServiceBusProcessorOptions ProcessorOptions { get; set; } = new();
    public ServiceBusSessionProcessorOptions? SessionProcessorOptions { get; set; }  // enables session mode
    public ServiceBusMessageAttributeNames AttributeNames { get; init; } = new();
    public Func<ProcessErrorEventArgs, Task>? ErrorHandler { get; init; }
}
```

### Queue or Topic targets

Three implementations of `IQueueOrTopic`:

```csharp
// Subscribe to a queue
new Queue("my-queue")

// Subscribe to a topic (uses SubscriptionId from options as the subscription name)
new Topic("my-topic")

// Subscribe to a topic with an explicit subscription name
new TopicAndSubscription("my-topic", "my-subscription")
```

### Session support

When `SessionProcessorOptions` is set, the subscription uses `ServiceBusSessionProcessor` instead of `ServiceBusProcessor`, enabling ordered processing per session:

```csharp
services.AddSubscription<ServiceBusSubscription, ServiceBusSubscriptionOptions>(
    "SessionSub",
    builder => builder
        .Configure(o => {
            o.QueueOrTopic = new Queue("session-queue");
            o.SessionProcessorOptions = new ServiceBusSessionProcessorOptions();
        })
        .AddEventHandler<MyHandler>()
);
```

### Message attribute names

`ServiceBusMessageAttributeNames` controls how metadata maps to Service Bus properties:

```csharp
public class ServiceBusMessageAttributeNames {
    public string MessageType   { get; set; } = "MessageType";
    public string StreamName    { get; set; } = "StreamName";
    public string CorrelationId { get; set; } = "correlation-id";
    public string CausationId   { get; set; } = "causation-id";
    public string ReplyTo       { get; set; } = "ReplyTo";
    public string Subject       { get; set; } = "Subject";
    public string To            { get; set; } = "To";
    public string MessageId     { get; set; } = "message-id";
}
```

## Complete Example

```csharp
// Register the Azure SDK client
// Do not hardcode credentials; use secret storage or environment variables
services.AddSingleton(new ServiceBusClient(configuration["AzureServiceBus:ConnectionString"]!));

// Register producer
services.AddProducer<ServiceBusProducer>(sp =>
    new ServiceBusProducer(
        sp.GetRequiredService<ServiceBusClient>(),
        new ServiceBusProducerOptions { QueueOrTopicName = "events-topic" }
    )
);

// Register subscription from a queue
services.AddSubscription<ServiceBusSubscription, ServiceBusSubscriptionOptions>(
    "MyQueueSubscription",
    builder => builder
        .Configure(o => {
            o.QueueOrTopic = new Queue("events-queue");
        })
        .AddEventHandler<MyEventHandler>()
);

// Register subscription from a topic with explicit subscription
services.AddSubscription<ServiceBusSubscription, ServiceBusSubscriptionOptions>(
    "MyTopicSubscription",
    builder => builder
        .Configure(o => {
            o.QueueOrTopic = new TopicAndSubscription("events-topic", "my-sub");
        })
        .AddEventHandler<MyEventHandler>()
);
```

## Tracing

Built-in OpenTelemetry tracing with `MessagingSystem = "azure-service-bus"`.
