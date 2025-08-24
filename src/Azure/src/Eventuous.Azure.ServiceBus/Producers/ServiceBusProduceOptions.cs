// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Azure.ServiceBus.Producers;

/// <summary>
/// Represents options for producing messages to Azure Service Bus.
/// </summary>
public class ServiceBusProduceOptions {
    /// <summary>
    /// Gets or sets the subject of the message.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the destination address for the message.
    /// </summary>
    public string? To { get; set; }

    /// <summary>
    /// Gets or sets the address to which replies should be sent.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the time interval after which the message expires.
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.MaxValue;
}