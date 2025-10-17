// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.CodeAnalysis;

namespace Eventuous.Extensions.AspNetCore.Generators;

internal static class Diagnostics {
    public const string DiagnosticCategory = "Eventuous.Extensions.AspNetCore";

    internal static readonly DiagnosticDescriptor StateMatchRule = new(
        "EVTA001",
        "HttpCommand state type mismatches MapCommands state",
        "Command {0} is mapped to state {1} but the route builder is for state {2}",
        DiagnosticCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When using MapCommands<TState>().MapCommand<TContract, TCommand>(...), the TContract decorated with HttpCommandAttribute<T> must have T matching the TState of the route builder."
    );

    internal static readonly DiagnosticDescriptor RouteRule = new(
        "EVTA002",
        "HttpCommand route override mismatches attribute route",
        "Command {0} attribute route '{1}' does not match route override '{2}'",
        DiagnosticCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When an HttpCommandAttribute specifies a Route and MapCommand is called with an explicit route override, the values should match."
    );

    internal static readonly DiagnosticDescriptor DuplicateRouteDiagnostic = new(
        id: "EVTA003",
        title: "Duplicate route detected",
        messageFormat: "Duplicate route detected: {0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    internal static readonly DiagnosticDescriptor ParentStateMatchDiagnostic = new(
        id: "EVTA004",
        title: "State type mismatch",
        messageFormat: "Command '{0}' state type '{1}' doesn't match with parent state type '{2}'",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
