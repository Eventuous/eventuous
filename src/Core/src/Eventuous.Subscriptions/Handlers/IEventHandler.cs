// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

using Context;

/// <summary>
/// Subscription event handlers must implement this interface
/// </summary>
public interface IEventHandler {
    /// <summary>
    /// Event handler name that is used for diagnostics (logging, tracing, etc)
    /// </summary>
    string DiagnosticName { get; }

    /// <summary>
    /// Function that handles an event received from the subscription
    /// </summary>
    /// <param name="context">Message context with message payload and details</param>
    /// <returns></returns>
    ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}
