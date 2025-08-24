// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Azure.ServiceBus.Shared;

/// <summary>
/// The attribute names used in <see cref="Metadata"/> to map to Service Bus message properties.
/// </summary>
public class ServiceBusMessageAttributeNames {
    /// <summary>
    /// The message type attribute name.
    /// </summary>
    public string MessageType { get; set; } = nameof(MessageType);

    /// <summary>
    /// The stream name attribute name.
    /// </summary>
    public string StreamName { get; set; } = nameof(StreamName);

    /// <summary>
    /// The correlation ID attribute name.
    /// </summary>
    public string CorrelationId { get; set; } = MetaTags.CorrelationId;

    /// <summary>
    /// The causation ID attribute name.
    /// </summary>
    public string CausationId { get; set; } = MetaTags.CausationId;

    /// <summary>
    /// The reply-to address attribute name.
    /// </summary>
    public string ReplyTo { get; set; } = nameof(ReplyTo);

    /// <summary>
    /// The subject attribute name.
    /// </summary>
    public string Subject { get; set; } = nameof(Subject);

    /// <summary>
    /// The recipient address attribute name.
    /// </summary>
    public string To { get; set; } = nameof(To);

    /// <summary>
    /// The message ID attribute name.
    /// </summary>
    public string MessageId { get; set; } = MetaTags.MessageId;
}
