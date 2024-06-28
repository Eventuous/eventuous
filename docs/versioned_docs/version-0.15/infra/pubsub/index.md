---
title: "Google PubSub"
description: "Producers and subscriptions for Google Pub/Sub"
sidebar_position: 6
---

Pub/Sub is an asynchronous and scalable messaging service that decouples services producing messages from services processing those messages. [source](https://cloud.google.com/pubsub/docs/overview).

Eventuous supports producing and consuming messages using Google Pub/Sub. Use the `Eventuous.GooglePubSub` NuGet package to get started.

## Producer

The `GooglePubSubProducer` class allows you to produce messages to any topic in a GCP project. When creating an instance of the producer you need to provide the project id and, optionally, configure the client using the `ConfigureClient` delegate. 

The `CreateTopic` option tells the producer to create a topic if it does not exist. Creating a topic is a one-time operation, so you can set this option to `false` after the topic is created. Often, you would have a separate process that creates topics and subscriptions, so the `CreateTopic` option is set to `false` by default.

```csharp
// Using options
var producer = new GooglePubSubProducer(
    new PubSubProducerOptions {
        ProjectId       = "my-gcp-project",
        ConfigureClient = b => b.EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
        CreateTopic     = true
    }
);
```

You can register the producer in the service collection using the `AddProducer` extension method. It's recommended to go that way because Google Pub/Sub producer needs to be started and shut down following the application lifecycle, which is done automatically when you use the `AddProducer` registration helper.

Behind the scenes, the producer will create a separate Pub/Sub client instance per topic, as the Pub/Sub API only allows to use one client to work with a single topic. The producer will cache the client instances, so you don't need to worry about creating too many clients.

Producing messages is done using the `Produce` method. You can produce a single message or a batch of messages. The `Produce` method returns a `Task` that completes when the message is produced.

```csharp
await producer.Produce("my-topic", new MyMessage { ... });
```

It's also possible to add metadata to the produced message, which will be transferred as a set of headers. In addition, you can add the ordering key option, so you get messages with the same ordering keys consumed in the same order.

```csharp
var message = new MyMessage { ... };
var metadata = new Metadata { ["key"] = "value" };
await producer.Produce("my-topic", message, metadata, new PubSubProduceOptions { OrderingKey = message.TenantId });
```

## Subscription

Eventuous allows you to consume messages from Google Pub/Sub, which are published by other services using the Eventuous Pub/Sub producer. Theoretically, it can also consume messages from other producers, but it expects the message to have the `eventType` and `contentType` attributes set, which might not be the case for other producers. However, if an external producer sets some attributes that can be used for the event type and content type, you can override the default attribute names in subscription options.

Normally, you'd add the subscription to the service collection using `AddSubscription` extension method, as any other Eventuous subscription.

```csharp
services
    .AddSubscription<GooglePubSubSubscription, PubSubSubscriptionOptions>(
        "pubsub-subscription",
        builder => builder
            .Configure(
                x => {
                    x.ProjectId          = "my-gcp-project";
                    x.TopicId            = "my-topic";
                    x.CreateSubscription = true;
                }
            )
            .AddEventHandler<TestHandler>()
    );
```

Similarly to the producer, the Eventuous subscription can create a Pub/Sub subscription if it does not exist. Creating a subscription is a one-time operation, so you can set this option to `false` after the subscription is created. Often, you would have a separate process that creates topics and subscriptions, so the `CreateSubscription` option is set to `false` by default. 

If you expect to consume messages from non-Eventuous producers that provide event type and content type information in message attributes, but those attribute names don't match Eventuous conventions, you can override those attribute names in subscription options.

```csharp
options.Attributes = new PubSubAttributes {
    EventType   = "event-type",
    ContentType = "content-type"
};
```

## Cloud Run subscription

Google Pub/Sub messages can be used as Cloud Run triggers. Eventuous supports a lightweight subscription that can be used with the Cloud Run trigger. Use the `Eventuous.GooglePubSub.CloudRun` NuGet package to use it.

Essentially, the Cloud Run subscription creates an HTTP endpoint conforming the Pub/Sub trigger API. It allows your application to receive Pub/Sub messages as HTTP requests.

Cloud Run subscription set up is the same as for any other subscription, but it requires one additional step to map the HTTP endpoint.

```csharp
builder.Services.AddSubscription<CloudRunPubSubSubscription, CloudRunPubSubSubscriptionOptions>(
    "WebhookEvents",
    b => {
        b.Configure(o => o.TopicId = "webhook-events"); // Topc id is not used except for popualing the stream name
        b.AddEventHandlerWithRetries<WebhookEventHandler>(Policy.Handle<Exception>().RetryAsync(3));
    }
);

var app = builder.Build();
app.MapCloudRunPubSubSubscription("/");
```

The `MapCloudRunPubSubSubscription` extension method maps the subscription HTTP endpoint to the specified path. The path you provide there must match the path of your Cloud Run trigger for Pub/Sub, which you specified as the push endpoint. The push endpoint URL should be composed of your Cloud Run workload URL, combined with the path configured by `MapCloudRunPubSubSubscription`.  Root path is the default value.

Read mode about configuring push subscriptions in the [Google Cloud Run documentation](https://cloud.google.com/run/docs/triggering/pubsub-push#create-push-subscription).