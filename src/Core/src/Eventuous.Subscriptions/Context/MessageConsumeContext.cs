// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

using Logging;

public class MessageConsumeContext(
        string            eventId,
        string            eventType,
        string            contentType,
        string            stream,
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
    public string            MessageId         { get; } = eventId;
    public string            MessageType       { get; } = eventType;
    public string            ContentType       { get; } = contentType;
    public StreamName        Stream            { get; } = new(stream);
    public ulong             StreamPosition    { get; } = streamPosition;
    public ulong             GlobalPosition    { get; } = globalPosition;
    public DateTime          Created           { get; } = created;
    public Metadata?         Metadata          { get; } = metadata;
    public object?           Message           { get; } = message;
    public ContextItems      Items             { get; } = new();
    public ActivityContext?  ParentContext     { get; set; }
    public HandlingResults   HandlingResults   { get; }      = new();
    public CancellationToken CancellationToken { get; set; } = cancellationToken;
    public ulong             Sequence          { get; }      = sequence;
    public string            SubscriptionId    { get; }      = subscriptionId;
    public LogContext        LogContext        { get; set; } = Logger.Current;
}

public class MessageConsumeContext<T>(IMessageConsumeContext innerContext) : WrappedConsumeContext(innerContext), IMessageConsumeContext<T>
    where T : class {
    [PublicAPI]
    public new T Message => (T)InnerContext.Message!;
}
