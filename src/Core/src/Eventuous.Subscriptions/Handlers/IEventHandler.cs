// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions; 

public interface IEventHandler {
    string DiagnosticName { get; }
    
    ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}