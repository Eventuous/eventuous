// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

using Logging;

public abstract class WrappedConsumeContext(IMessageConsumeContext innerContext) : IMessageConsumeContext {
    public IMessageConsumeContext InnerContext { get; } = innerContext;

    public string          MessageId       => InnerContext.MessageId;
    public string          MessageType     => InnerContext.MessageType;
    public string          ContentType     => InnerContext.ContentType;
    public StreamName      Stream          => InnerContext.Stream;
    public ulong           EventNumber     => InnerContext.EventNumber;
    public ulong           StreamPosition  => InnerContext.StreamPosition;
    public ulong           GlobalPosition  => InnerContext.GlobalPosition;
    public DateTime        Created         => InnerContext.Created;
    public object?         Message         => InnerContext.Message;
    public Metadata?       Metadata        => InnerContext.Metadata;
    public ContextItems    Items           => InnerContext.Items;
    public HandlingResults HandlingResults => InnerContext.HandlingResults;
    public ulong           Sequence        => InnerContext.Sequence;
    public string          SubscriptionId  => InnerContext.SubscriptionId;
    public LogContext LogContext {
        get => InnerContext.LogContext;
        set => InnerContext.LogContext = value;
    }

    public CancellationToken CancellationToken {
        get => InnerContext.CancellationToken;
        set => InnerContext.CancellationToken = value;
    }

    public ActivityContext? ParentContext {
        get => InnerContext.ParentContext;
        set => InnerContext.ParentContext = value;
    }
}
