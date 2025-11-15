// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.CodeAnalysis;

namespace Eventuous.Extensions.AspNetCore.Generators;

internal static class Diagnostics {
    public const string DiagnosticCategory = "Eventuous.Extensions.AspNetCore";

    public const string DiagnosticId          = "EVTA001";
    public const string RouteDiagnosticId     = "EVTA002";
    public const string DuplicateRouteId      = "EVTA003";
    public const string ParentStateMismatchId = "EVTA004";

    internal static readonly DiagnosticDescriptor StateMatchRule = new(
        DiagnosticId,
        "HttpCommand state type mismatches MapCommands state",
        "Command {0} is mapped to state {1} but the route builder is for state {2}",
        DiagnosticCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When using MapCommands<TState>().MapCommand<TContract, TCommand>(...), the TContract decorated with HttpCommandAttribute<T> must have T matching the TState of the route builder."
    );

    internal static readonly DiagnosticDescriptor RouteRule = new(
        RouteDiagnosticId,
        "HttpCommand route override mismatches attribute route",
        "Command {0} attribute route '{1}' does not match route override '{2}'",
        DiagnosticCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When an HttpCommandAttribute specifies a Route and MapCommand is called with an explicit route override, the values should match."
    );

    internal static readonly DiagnosticDescriptor DuplicateRouteDiagnostic = new(
        id: DuplicateRouteId,
        title: "Duplicate route detected",
        messageFormat: "Duplicate route detected: {0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    internal static readonly DiagnosticDescriptor ParentStateMatchDiagnostic = new(
        id: ParentStateMismatchId,
        title: "State type mismatch",
        messageFormat: "Command '{0}' state type '{1}' doesn't match with parent state type '{2}'",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
