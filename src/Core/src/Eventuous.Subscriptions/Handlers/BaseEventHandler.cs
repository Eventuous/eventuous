// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions; 

public abstract class BaseEventHandler : IEventHandler {
    protected BaseEventHandler() => DiagnosticName = GetType().Name;
    
    public string DiagnosticName { get; }

    public abstract ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}