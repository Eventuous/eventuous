// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Eventuous.Shared.Generators.Constants;

// ReSharper disable CognitiveComplexity

namespace Eventuous.Shared.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventUsageAnalyzer : DiagnosticAnalyzer {
    public const string DiagnosticId = "EVTC001";

    static readonly DiagnosticDescriptor MissingEventTypeAttribute = new(
        id: DiagnosticId,
        title: "Event type is not decorated with [EventType]",
        messageFormat: "Event type '{0}' is used as a domain event but isn't annotated with [EventType]",
        category: "Eventuous",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Domain events should be annotated with [EventType] so they can be resolved by the type mapper."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingEventTypeAttribute];

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    static void AnalyzeInvocation(OperationAnalysisContext ctx) {
        if (ctx.Operation is not IInvocationOperation inv) return;

        var method = inv.TargetMethod;

        switch (method) {
            // Case 1: Aggregate<T>.Apply<TEvent>(TEvent evt)
            case { Name: "Apply", TypeArguments.Length: 1, Parameters.Length: 1 }: {
                var containing = method.ContainingType;

                if (IsAggregate(containing)) {
                    var eventType = method.TypeArguments[0];

                    if (IsConcreteEvent(eventType) && !HasEventTypeAttribute(eventType)) {
                        ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, inv.Syntax.GetLocation(), eventType.ToDisplayString()));
                    }
                }

                return;
            }
            // Case 1b: State<T>.When(...) invocations where an event instance is passed
            case { Name: "When", Parameters.Length: 1 } when IsState(method.ContainingType): {
                var arg = inv.Arguments.Length > 0 ? inv.Arguments[0].Value : null;

                ITypeSymbol? eventType = null;

                if (method.TypeArguments.Length == 1) {
                    eventType = method.TypeArguments[0];
                }

                eventType ??= arg switch {
                    IConversionOperation { Operand.Type: not null } conv => conv.Operand.Type,
                    _                                                    => arg?.Type
                };

                if (eventType != null && IsConcreteEvent(eventType) && !HasEventTypeAttribute(eventType)) {
                    var location = arg?.Syntax.GetLocation() ?? inv.Syntax.GetLocation();
                    ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, location, eventType.ToDisplayString()));
                }

                return;
            }
            // Case 1c: State<T>.On<TEvent>(...) handler registrations
            case { Name: "On", TypeArguments.Length: 1 } when IsState(method.ContainingType): {
                var eventType = method.TypeArguments[0];

                if (IsConcreteEvent(eventType) && !HasEventTypeAttribute(eventType)) {
                    ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, inv.Syntax.GetLocation(), eventType.ToDisplayString()));
                }

                return;
            }
        }

        // Case 2: Functional service: Act/ActAsync handlers
        if (method.Name is "Act" or "ActAsync") {
            // Heuristic: only consider the overloads that accept a delegate and are defined in CommandHandlerBuilder interfaces/classes
            if (!IsFunctionalServiceAct(method)) return;

            foreach (var value in inv.Arguments.Select(arg => arg.Value)) {
                switch (value) {
                    case null:
                        continue;
                    // If the argument is a lambda, analyze its body for created event instances
                    case IAnonymousFunctionOperation lambda:
                        AnalyzeDelegateBodyForEventCreations(ctx, lambda.Body);

                        break;
                    case IConversionOperation { Operand: IAnonymousFunctionOperation lambdaConv }:
                        AnalyzeDelegateBodyForEventCreations(ctx, lambdaConv.Body);

                        break;
                }
            }
        }
    }

    static void AnalyzeDelegateBodyForEventCreations(OperationAnalysisContext ctx, IBlockOperation? body) {
        if (body is null) return;

        foreach (var op in body.Descendants()) {
            if (op is IObjectCreationOperation create) {
                var created = create.Type;

                if (created != null && IsConcreteEvent(created) && !HasEventTypeAttribute(created)) {
                    ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, create.Syntax.GetLocation(), created.ToDisplayString()));
                }
            }
        }
    }

    static void AnalyzeObjectCreation(OperationAnalysisContext ctx) {
        // Global safety net for method groups passed into Act where we couldn't traverse the body via the invocation site.
        // If the object creation is within a method that appears to be an Act handler (returns NewEvents/ IEnumerable<object>), warn.
        if (ctx.Operation is not IObjectCreationOperation create) return;

        var created = create.Type;

        if (created is null || !IsConcreteEvent(created)) return;

        var method = GetEnclosingMethod(ctx.Operation);

        if (method == null) return;

        if (ReturnsNewEvents(method)) {
            if (!HasEventTypeAttribute(created)) {
                ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, create.Syntax.GetLocation(), created.ToDisplayString()));
            }
        }
    }

    static IMethodSymbol? GetEnclosingMethod(IOperation op) {
        for (var p = op.Parent; p != null; p = p.Parent) {
            switch (p) {
                case IAnonymousFunctionOperation anon:
                    return anon.Symbol;
                case ILocalFunctionOperation local:
                    return local.Symbol;
                case IMethodBodyOperation body:
                    return body.SemanticModel?.GetEnclosingSymbol(body.Syntax.SpanStart) as IMethodSymbol;
            }
        }

        return null;
    }

    static bool ReturnsNewEvents(IMethodSymbol method) {
        // NewEvents is a global alias for IEnumerable<object> within Eventuous; we’ll detect either the alias name or the underlying type
        var ret = method.ReturnType;

        return ret switch {
            null => false,
            // Check if it's an array
            IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Object } => true,
            // Check name first (alias would appear as IEnumerable<object> in symbols, so rely on namespace/type)
            INamedTypeSymbol named when IsIEnumerableOfObject(named) => true,
            _                                                        => false
        };
    }

    static bool IsIEnumerableOfObject(INamedTypeSymbol type) {
        if (type.Name == "IEnumerable" && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" && type.TypeArguments.Length == 1) {
            return type.TypeArguments[0] is { SpecialType: SpecialType.System_Object };
        }

        return false;
    }

    static bool IsAggregate(INamedTypeSymbol? type) {
        if (type == null) return false;

        // Walk base types to check if it derives from Eventuous.Aggregate<>
        for (var t = type; t != null; t = t.BaseType) {
            if (t is { Name: "Aggregate", Arity: 1 } && t.ContainingNamespace.ToDisplayString() == BaseNamespace) return true;
        }

        return false;
    }

    static bool IsState(INamedTypeSymbol? type) {
        if (type == null) return false;

        // Walk base types to check if it derives from Eventuous.State<>
        for (var t = type; t != null; t = t.BaseType) {
            if (t is { Name: "State", Arity: 1 } && t.ContainingNamespace.ToDisplayString() == BaseNamespace) return true;
        }

        return false;
    }

    static bool IsFunctionalServiceAct(IMethodSymbol method) {
        // We only care about the Act methods from CommandHandlerBuilder and the related interfaces in Eventuous namespace
        if (method.Name is not ("Act" or "ActAsync")) return false;

        var containing = method.ContainingType;

        if (containing == null) return false;

        var ns = containing.ContainingNamespace?.ToDisplayString();

        if (ns != BaseNamespace) return false;

        // Simple name checks
        return containing.Name is "CommandHandlerBuilder" or "IDefineExecution" or "ICommandHandlerBuilder" or "IDefineStoreOrExecution";
    }

    static bool IsConcreteEvent(ITypeSymbol type) => type.TypeKind is TypeKind.Class or TypeKind.Struct;

    static bool HasEventTypeAttribute(ITypeSymbol type)
        => (from attrClass in type.GetAttributes().Select(a => a.AttributeClass).OfType<INamedTypeSymbol>()
            let name = attrClass.ToDisplayString()
            where name == EventTypeAttrFqcn || attrClass.Name is EventTypeAttribute
            select attrClass).Any();
}
