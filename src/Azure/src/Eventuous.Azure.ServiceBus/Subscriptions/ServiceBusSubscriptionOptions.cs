// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Azure.ServiceBus.Shared;
using Eventuous.Subscriptions;

namespace Eventuous.Azure.ServiceBus.Subscriptions;

/// <summary>
/// Options for configuring a Service Bus subscription.
/// </summary>
public record ServiceBusSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Gets or sets the queue or topic to subscribe to.
    /// </summary>
    public required IQueueOrTopic QueueOrTopic { get; set; }

    /// <summary>
    /// Gets or sets the options for the Service Bus processor.
    /// </summary>
    public ServiceBusProcessorOptions ProcessorOptions { get; set; } = new();

    /// <summary>
    /// Gets the message attributes for Service Bus messages.
    /// </summary>
    public ServiceBusMessageAttributeNames AttributeNames { get; init; } = new();

    /// <summary>
    /// Gets the error handler delegate for processing errors.
    /// </summary>
    public Func<ProcessErrorEventArgs, Task>? ErrorHandler { get; init; }
}

/// <summary>
/// Represents a queue or topic for Service Bus subscriptions.
/// </summary>
public interface IQueueOrTopic {
    /// <summary>
    /// Creates a <see cref="ServiceBusProcessor"/> for the specified client and options.
    /// </summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="options">The subscription options.</param>
    /// <returns>A configured <see cref="ServiceBusProcessor"/> instance.</returns>
    ServiceBusProcessor MakeProcessor(ServiceBusClient client, ServiceBusSubscriptionOptions options);
}

/// <summary>
/// Represents a Service Bus queue.
/// </summary>
public record Queue(string Name) : IQueueOrTopic {
    /// <summary>
    /// Creates a <see cref="ServiceBusProcessor"/> for the queue.
    /// </summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="options">The subscription options.</param>
    /// <returns>A configured <see cref="ServiceBusProcessor"/> for the queue.</returns>
    public ServiceBusProcessor MakeProcessor(ServiceBusClient client, ServiceBusSubscriptionOptions options) 
        => client.CreateProcessor(Name, options.ProcessorOptions);
}

/// <summary>
/// Represents a Service Bus topic.
/// </summary>
public record Topic(string Name) : IQueueOrTopic {
    /// <summary>
    /// Creates a <see cref="ServiceBusProcessor"/> for the topic and subscription ID from options.
    /// </summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="options">The subscription options.</param>
    /// <returns>A configured <see cref="ServiceBusProcessor"/> for the topic.</returns>
    public ServiceBusProcessor MakeProcessor(ServiceBusClient client, ServiceBusSubscriptionOptions options) 
        => client.CreateProcessor(Name, options.SubscriptionId, options.ProcessorOptions);
}

/// <summary>
/// Represents a Service Bus topic and a specific subscription.
/// </summary>
public record TopicAndSubscription(string Name, string Subscription) : IQueueOrTopic {
    /// <summary>
    /// Creates a <see cref="ServiceBusProcessor"/> for the topic and specified subscription.
    /// </summary>
    /// <param name="client">The Service Bus client.</param>
    /// <param name="options">The subscription options.</param>
    /// <returns>A configured <see cref="ServiceBusProcessor"/> for the topic and subscription.</returns>
    public ServiceBusProcessor MakeProcessor(ServiceBusClient client, ServiceBusSubscriptionOptions options)
        => client.CreateProcessor(Name, Subscription, options.ProcessorOptions);
}
