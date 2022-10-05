// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Subscriptions.Context;

public class MessageConsumeContext : IMessageConsumeContext {
    public MessageConsumeContext(
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
    ) {
        MessageId         = eventId;
        MessageType       = eventType;
        ContentType       = contentType;
        Stream            = new StreamName(stream);
        StreamPosition    = streamPosition;
        GlobalPosition    = globalPosition;
        Created           = created;
        Metadata          = metadata;
        Sequence          = sequence;
        Message           = message;
        CancellationToken = cancellationToken;
        SubscriptionId    = subscriptionId;
        LogContext        = Logger.Current;
    }

    public string            MessageId         { get; }
    public string            MessageType       { get; }
    public string            ContentType       { get; }
    public StreamName        Stream            { get; }
    public ulong             StreamPosition    { get; }
    public ulong             GlobalPosition    { get; }
    public DateTime          Created           { get; }
    public Metadata?         Metadata          { get; }
    public object?           Message           { get; }
    public ContextItems      Items             { get; } = new();
    public ActivityContext?  ParentContext     { get; set; }
    public HandlingResults   HandlingResults   { get; } = new();
    public CancellationToken CancellationToken { get; set; }
    public ulong             Sequence          { get; }
    public string            SubscriptionId    { get; }
    public LogContext        LogContext        { get; set; }
}

public class MessageConsumeContext<T> : WrappedConsumeContext, IMessageConsumeContext<T>
    where T : class {
    public MessageConsumeContext(IMessageConsumeContext innerContext) : base(innerContext) { }

    [PublicAPI]
    public new T Message => (T)InnerContext.Message!;
}
