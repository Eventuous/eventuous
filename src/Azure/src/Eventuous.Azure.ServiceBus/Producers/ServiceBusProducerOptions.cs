// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Azure.ServiceBus.Producers;

using Shared;

/// <summary>
/// Options for configuring a Service Bus producer, including the target queue or topic, sender options, and message attributes.
/// </summary>
public class ServiceBusProducerOptions {
    /// <summary>
    /// Gets or sets the name of the queue or topic to which messages will be sent.
    /// </summary>
    public required string QueueOrTopicName { get; init; }

    /// <summary>
    /// Gets or sets the options for configuring the Service Bus sender.
    /// </summary>
    public ServiceBusSenderOptions? SenderOptions { get; init; }

    /// <summary>
    /// Gets the attributes to be applied to outgoing Service Bus messages.
    /// </summary>
    public ServiceBusMessageAttributeNames AttributeNames { get; init; } = new();
}
