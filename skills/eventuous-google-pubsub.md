# Eventuous Google Pub/Sub Integration

NuGet package: `Eventuous.GooglePubSub`
Namespace: `Eventuous.GooglePubSub.Producers`, `Eventuous.GooglePubSub.Subscriptions`
Source: `src/GooglePubSub/src/Eventuous.GooglePubSub/`

## Producer

`GooglePubSubProducer` extends `BaseProducer<PubSubProduceOptions>` and implements `IHostedProducer`.

### Constructor overloads

```csharp
// Simple: project ID only
new GooglePubSubProducer(projectId: "my-gcp-project");

// Full options
new GooglePubSubProducer(new PubSubProducerOptions {
    ProjectId = "my-gcp-project",
    ConfigureClientBuilder = builder => {
        builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
    },
    CreateTopic = true,   // default: true, auto-creates topic
    Attributes = new PubSubAttributes {
        EventType   = "eventType",    // defaults
        ContentType = "contentType",
        MessageId   = "messageId"
    }
});

// IOptions<PubSubProducerOptions> overload for DI
new GooglePubSubProducer(IOptions<PubSubProducerOptions> options);
```

### PubSubProducerOptions

```csharp
public class PubSubProducerOptions {
    public string ProjectId { get; init; } = null!;
    public Action<PublisherClientBuilder>? ConfigureClientBuilder { get; init; }
    public PubSubAttributes Attributes { get; init; } = new();
    public bool CreateTopic { get; set; } = true;   // set false if pre-created
}
```

### PubSubProduceOptions (per-message)

```csharp
public class PubSubProduceOptions {
    public Func<object, MapField<string, string>>? AddAttributes { get; init; }
    public string? OrderingKey { get; init; }  // requires client configured for ordering
}
```

### How it works

- Caches `PublisherClient` instances per topic via `ClientCache`
- Auto-creates topics if `CreateTopic = true` (requires Pub/Sub admin permissions)
- Event type, content type, and message ID stored as Pub/Sub message attributes
- Metadata entries added as additional message attributes
- `StopAsync` shuts down all cached publisher clients

### DI Registration

```csharp
services.AddProducer<GooglePubSubProducer>(sp =>
    new GooglePubSubProducer(new PubSubProducerOptions {
        ProjectId = "my-gcp-project"
    })
);

// Or with IOptions pattern
services.Configure<PubSubProducerOptions>(config.GetSection("PubSub"));
services.AddProducer<GooglePubSubProducer>();
```

## Subscription

`GooglePubSubSubscription` extends `EventSubscription<PubSubSubscriptionOptions>`.

### Constructor overloads

```csharp
// Simple constructor
new GooglePubSubSubscription(
    projectId: "my-gcp-project",
    topicId: "my-topic",
    subscriptionId: "my-subscription",
    consumePipe: pipe,
    loggerFactory: loggerFactory,
    eventSerializer: serializer,
    configureClient: builder => { ... }
);

// Options-based constructor
new GooglePubSubSubscription(options, consumePipe, loggerFactory, eventSerializer);
```

### PubSubSubscriptionOptions

```csharp
public record PubSubSubscriptionOptions : SubscriptionOptions {
    public string ProjectId { get; set; } = null!;
    public string TopicId { get; set; } = null!;
    public bool CreateSubscription { get; set; } = true;  // auto-create
    public Action<SubscriberClientBuilder>? ConfigureClientBuilder { get; set; }
    public HandleEventProcessingFailure? FailureHandler { get; set; }  // default: NACK
    public Action<Subscription>? ConfigureSubscription { get; set; }
    public PubSubAttributes Attributes { get; set; } = new();
}
```

### Failure handling

```csharp
// Custom failure handler delegate
public delegate ValueTask<Reply> HandleEventProcessingFailure(
    SubscriberClient client,
    PubsubMessage pubsubMessage,
    Exception exception
);
```

Default behavior is `Reply.Nack`. You can override to implement dead-letter logic or `Reply.Ack` to skip poison messages.

### DI Registration

```csharp
services.AddSubscription<GooglePubSubSubscription, PubSubSubscriptionOptions>(
    "MyPubSubSubscription",
    builder => builder
        .Configure(o => {
            o.ProjectId = "my-gcp-project";
            o.TopicId = "my-topic";
        })
        .AddEventHandler<MyHandler>()
);
```

## Emulator Support

Configure via `EmulatorDetection` on the client builder:

```csharp
services.AddProducer<GooglePubSubProducer>(sp =>
    new GooglePubSubProducer(new PubSubProducerOptions {
        ProjectId = "my-project",
        ConfigureClientBuilder = builder => {
            builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
        }
    })
);
```

Set the `PUBSUB_EMULATOR_HOST` environment variable to point to the emulator.

## Message Attributes

`PubSubAttributes` controls the Pub/Sub message attribute names for system values:

```csharp
public class PubSubAttributes {
    public string EventType   { get; set; } = "eventType";
    public string ContentType { get; set; } = "contentType";
    public string MessageId   { get; set; } = "messageId";
}
```

## Tracing

Built-in OpenTelemetry tracing with `MessagingSystem = "google-pubsub"`.
