// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

using Logging;

public class MessageConsumeContext(
        string            eventId,
        string            eventType,
        string            contentType,
        string            stream,
        ulong             eventNumber,
        ulong             streamPosition,
        ulong             globalPosition,
        ulong             sequence,
        DateTime          created,
        object?           message,
        Metadata?         metadata,
        string            subscriptionId,
        CancellationToken cancellationToken
    )
    : IMessageConsumeContext {
    /// <inheritdoc />
    public string            MessageId         { get; } = eventId;
    /// <inheritdoc />
    public string            MessageType       { get; } = eventType;
    /// <inheritdoc />
    public string            ContentType       { get; } = contentType;
    /// <inheritdoc />
    public StreamName        Stream            { get; } = new(stream);
    /// <inheritdoc />
    public ulong             EventNumber       { get; } = eventNumber;
    /// <inheritdoc />
    public ulong             StreamPosition    { get; } = streamPosition;
    /// <inheritdoc />
    public ulong             GlobalPosition    { get; } = globalPosition;
    /// <inheritdoc />
    public DateTime          Created           { get; } = created;
    /// <inheritdoc />
    public Metadata?         Metadata          { get; } = metadata;
    /// <inheritdoc />
    public object?           Message           { get; } = message;
    /// <inheritdoc />
    public ContextItems      Items             { get; } = new();
    /// <inheritdoc />
    public ActivityContext?  ParentContext     { get; set; }
    /// <inheritdoc />
    public HandlingResults   HandlingResults   { get; }      = new();
    /// <inheritdoc />
    public CancellationToken CancellationToken { get; set; } = cancellationToken;
    /// <inheritdoc />
    public ulong             Sequence          { get; }      = sequence;
    /// <inheritdoc />
    public string            SubscriptionId    { get; }      = subscriptionId;
    /// <inheritdoc />
    public LogContext        LogContext        { get; set; } = Logger.Current;
}

public class MessageConsumeContext<T>(IMessageConsumeContext innerContext) : WrappedConsumeContext(innerContext), IMessageConsumeContext<T>
    where T : class {
    [PublicAPI]
    public new T Message => (T)InnerContext.Message!;
}
