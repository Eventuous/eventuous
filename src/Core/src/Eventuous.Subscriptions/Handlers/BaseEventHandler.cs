// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

using Context;

public abstract class BaseEventHandler : IEventHandler {
    protected BaseEventHandler() => DiagnosticName = GetType().Name;

    public string DiagnosticName { get; }

    public abstract ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}
