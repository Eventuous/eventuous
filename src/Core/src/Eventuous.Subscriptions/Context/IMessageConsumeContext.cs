// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

using Logging;

/// <summary>
/// Base interface for a consume context, which doesn't include the payload.
/// </summary>
public interface IBaseConsumeContext {
    /// <summary>
    /// Unique identifier of the message
    /// </summary>
    string MessageId { get; }
    /// <summary>
    /// Message type delivered in the message metadata or headers
    /// </summary>
    string MessageType { get; }
    /// <summary>
    /// Message content type, e.g. application/json
    /// </summary>
    string            ContentType       { get; }
    /// <summary>
    /// Stream name where message was read from. When reading from a category or $all, this will be the original stream name.
    /// </summary>
    StreamName        Stream            { get; }
    /// <summary>
    /// Event number in the original event stream
    /// </summary>
    ulong             EventNumber       { get; }
    /// <summary>
    /// Position of the received message in the subscription stream. It can differ from <see cref="EventNumber"/> when reading from a category or $all.
    /// </summary>
    ulong             StreamPosition    { get; }
    /// <summary>
    /// Message position in the global log
    /// </summary>
    ulong             GlobalPosition    { get; }
    /// <summary>
    /// System metadata value of the message produce date and time in UTC
    /// </summary>
    DateTime          Created           { get; }
    /// <summary>
    /// Metadata stored alongside the message
    /// </summary>
    Metadata?         Metadata          { get; }
    /// <summary>
    /// Baggage items for the consume context
    /// </summary>
    ContextItems      Items             { get; }
    /// <summary>
    /// Diagnostic activity parent context (can be remote context)
    /// </summary>
    ActivityContext?  ParentContext     { get; set; }
    /// <summary>
    /// Collection of message handling results, expected one result per handler
    /// </summary>
    HandlingResults   HandlingResults   { get; }
    /// <summary>
    /// Cancellation token passed from the subscription
    /// </summary>
    CancellationToken CancellationToken { get; set; }
    /// <summary>
    /// Message sequence number in the subscription (can be maintained locally, in process)
    /// </summary>
    ulong             Sequence          { get; }
    /// <summary>
    /// Subscription identifier, can also be used as a checkpoint
    /// </summary>
    string            SubscriptionId    { get; }
    /// <summary>
    /// Logging context for the consume pipeline
    /// </summary>
    LogContext        LogContext        { get; set; }
}

public interface IMessageConsumeContext : IBaseConsumeContext {
    /// <summary>
    /// Deserialized message payload
    /// </summary>
    object? Message { get; }
}

public interface IMessageConsumeContext<out T> : IBaseConsumeContext where T : class {
    /// <summary>
    /// Deserialized message payload
    /// </summary>
    T Message { get; }
}
