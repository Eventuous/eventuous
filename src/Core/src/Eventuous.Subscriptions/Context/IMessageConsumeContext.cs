using System.Diagnostics;

namespace Eventuous.Subscriptions.Context;

/// <summary>
/// Base interface for a consume context, which doesn't include the payload.
/// </summary>
public interface IBaseConsumeContext {
    string            MessageId         { get; }
    string            MessageType       { get; }
    string            ContentType       { get; }
    string            Stream            { get; }
    DateTime          Created           { get; }
    Metadata?         Metadata          { get; }
    ContextItems      Items             { get; }
    ActivityContext?  ParentContext     { get; set; }
    HandlingResults   HandlingResults   { get; }
    CancellationToken CancellationToken { get; set; }
    ulong             Sequence          { get; }
    string            SubscriptionId    { get; }
}

public interface IMessageConsumeContext : IBaseConsumeContext {
    object? Message { get; }
}

public interface IMessageConsumeContext<out T> : IBaseConsumeContext where T : class {
    T? Message { get; }
}