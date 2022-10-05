// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Subscriptions.Context;

/// <summary>
/// Base interface for a consume context, which doesn't include the payload.
/// </summary>
public interface IBaseConsumeContext {
    string            MessageId         { get; }
    string            MessageType       { get; }
    string            ContentType       { get; }
    StreamName        Stream            { get; }
    ulong             StreamPosition    { get; }
    ulong             GlobalPosition    { get; }
    DateTime          Created           { get; }
    Metadata?         Metadata          { get; }
    ContextItems      Items             { get; }
    ActivityContext?  ParentContext     { get; set; }
    HandlingResults   HandlingResults   { get; }
    CancellationToken CancellationToken { get; set; }
    ulong             Sequence          { get; }
    string            SubscriptionId    { get; }
    LogContext        LogContext        { get; set; }
}

public interface IMessageConsumeContext : IBaseConsumeContext {
    object? Message { get; }
}

public interface IMessageConsumeContext<out T> : IBaseConsumeContext where T : class {
    T Message { get; }
}
