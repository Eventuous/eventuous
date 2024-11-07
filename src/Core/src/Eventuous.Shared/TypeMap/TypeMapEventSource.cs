// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;

namespace Eventuous;

using Diagnostics;

[EventSource(Name = DiagnosticName.BaseName)]
class TypeMapEventSource : EventSource {
    public static readonly TypeMapEventSource Log = new();

    const int TypeNotMappedToNameId      = 8;
    const int TypeNameNotMappedToTypeId  = 9;
    const int TypeMapRegisteredId        = 10;
    const int TypeMapAlreadyRegisteredId = 11;

    [NonEvent]
    public void TypeNotMappedToName(Type type) => TypeNotMappedToName(type.Name);

    [Event(TypeNotMappedToNameId, Message = "Type {0} is not registered in the type map", Level = EventLevel.Error)]
    public void TypeNotMappedToName(string type) => WriteEvent(TypeNotMappedToNameId, type);

    [Event(TypeNameNotMappedToTypeId, Message = "Type name {0} is not mapped to any type", Level = EventLevel.Error)]
    public void TypeNameNotMappedToType(string typeName) => WriteEvent(TypeNameNotMappedToTypeId, typeName);

    [Event(TypeMapRegisteredId, Message = "Type {0} registered as {1}", Level = EventLevel.Verbose)]
    public void TypeMapRegistered(string type, string typeName) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All)) WriteEvent(TypeMapRegisteredId, type, typeName);
    }

    [Event(TypeMapAlreadyRegisteredId, Message = "Type already {0} registered as {1}", Level = EventLevel.Verbose)]
    public void TypeAlreadyRegistered(string type, string typeName) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All)) WriteEvent(TypeMapAlreadyRegisteredId, type, typeName);
    }
}
