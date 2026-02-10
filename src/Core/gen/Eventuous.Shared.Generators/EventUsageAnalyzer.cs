// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        context.RegisterCompilationStartAction(compilationContext => {
            // Resolve well-known type symbols once per compilation
            var compilation = compilationContext.Compilation;
            var knownTypes = new KnownTypeSymbols(compilation);

            compilationContext.RegisterOperationAction(ctx => AnalyzeInvocation(ctx, knownTypes), OperationKind.Invocation);
            compilationContext.RegisterOperationAction(ctx => AnalyzeObjectCreation(ctx, knownTypes), OperationKind.ObjectCreation);
        });
    }

    /// <summary>
    /// Cache of well-known type symbols resolved from the compilation.
    /// This makes the analyzer refactoring-safe by using symbol comparison instead of string matching.
    /// </summary>
    sealed class KnownTypeSymbols {
        public INamedTypeSymbol? EventTypeAttribute { get; }
        public INamedTypeSymbol? TypeMapper { get; }
        public INamedTypeSymbol? Aggregate { get; }
        public INamedTypeSymbol? State { get; }
        public INamedTypeSymbol? CommandHandlerBuilder { get; }
        public INamedTypeSymbol? IDefineExecution { get; }
        public INamedTypeSymbol? ICommandHandlerBuilder { get; }
        public INamedTypeSymbol? IDefineStoreOrExecution { get; }

        public KnownTypeSymbols(Compilation compilation) {
            EventTypeAttribute = compilation.GetTypeByMetadataName(EventTypeAttrFqcn);
            TypeMapper = compilation.GetTypeByMetadataName($"{BaseNamespace}.TypeMapper");
            Aggregate = compilation.GetTypeByMetadataName($"{BaseNamespace}.Aggregate`1");
            State = compilation.GetTypeByMetadataName($"{BaseNamespace}.State`1");
            CommandHandlerBuilder = compilation.GetTypeByMetadataName($"{BaseNamespace}.CommandHandlerBuilder");
            IDefineExecution = compilation.GetTypeByMetadataName($"{BaseNamespace}.IDefineExecution");
            ICommandHandlerBuilder = compilation.GetTypeByMetadataName($"{BaseNamespace}.ICommandHandlerBuilder");
            IDefineStoreOrExecution = compilation.GetTypeByMetadataName($"{BaseNamespace}.IDefineStoreOrExecution");
        }
    }

    static ImmutableHashSet<ITypeSymbol> GetExplicitRegistrations(OperationAnalysisContext ctx, KnownTypeSymbols knownTypes) {
        var model = ctx.Operation.SemanticModel;
        if (model == null) return ImmutableHashSet<ITypeSymbol>.Empty;
        var root = ctx.Operation.Syntax.SyntaxTree.GetRoot();
        var set = ImmutableHashSet.CreateBuilder<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var invSyntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
            if (model.GetOperation(invSyntax) is not IInvocationOperation op) continue;
            var m = op.TargetMethod;

            // Use symbol comparison when available, fall back to string comparison
            if (m.Name != "AddType") continue;
            var ct = m.ContainingType;
            if (ct == null) continue;

            // Prefer symbol comparison (refactoring-safe)
            var isTypeMapper = knownTypes.TypeMapper != null
                ? SymbolEqualityComparer.Default.Equals(ct, knownTypes.TypeMapper)
                : ct.Name == "TypeMapper" && ct.ContainingNamespace?.ToDisplayString() == BaseNamespace;

            if (!isTypeMapper) continue;

            if (m.TypeArguments.Length == 1) {
                set.Add(m.TypeArguments[0]);
                continue;
            }

            if (op.Arguments.Length > 0 && op.Arguments[0].Value is ITypeOfOperation typeOfOp) {
                set.Add(typeOfOp.TypeOperand);
            }
        }

        return set.ToImmutable();
    }

    static bool IsExplicitlyRegistered(ITypeSymbol type, OperationAnalysisContext ctx, KnownTypeSymbols knownTypes) {
        var set = GetExplicitRegistrations(ctx, knownTypes);
        return set.Contains(type);
    }

    static void AnalyzeInvocation(OperationAnalysisContext ctx, KnownTypeSymbols knownTypes) {
        if (ctx.Operation is not IInvocationOperation inv) return;

        var method = inv.TargetMethod;

        switch (method) {
            // Case 1: Aggregate<T>.Apply<TEvent>(TEvent evt)
            case { Name: "Apply", TypeArguments.Length: 1, Parameters.Length: 1 }: {
                var containing = method.ContainingType;

                if (IsAggregate(containing, knownTypes)) {
                    var eventType = method.TypeArguments[0];

                    if (IsConcreteEvent(eventType) && !HasEventTypeAttribute(eventType, knownTypes) && !IsExplicitlyRegistered(eventType, ctx, knownTypes)) {
                        ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, inv.Syntax.GetLocation(), eventType.ToDisplayString()));
                    }
                }

                return;
            }
            // Case 1b: State<T>.When(...) invocations where an event instance is passed
            case { Name: "When", Parameters.Length: 1 } when IsState(method.ContainingType, knownTypes): {
                var arg = inv.Arguments.Length > 0 ? inv.Arguments[0].Value : null;

                ITypeSymbol? eventType = null;

                if (method.TypeArguments.Length == 1) {
                    eventType = method.TypeArguments[0];
                }

                eventType ??= arg switch {
                    IConversionOperation { Operand.Type: not null } conv => conv.Operand.Type,
                    _                                                    => arg?.Type
                };

                if (eventType != null && IsConcreteEvent(eventType) && !HasEventTypeAttribute(eventType, knownTypes) && !IsExplicitlyRegistered(eventType, ctx, knownTypes)) {
                    var location = arg?.Syntax.GetLocation() ?? inv.Syntax.GetLocation();
                    ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, location, eventType.ToDisplayString()));
                }

                return;
            }
            // Case 1c: State<T>.On<TEvent>(...) handler registrations
            case { Name: "On", TypeArguments.Length: 1 } when IsState(method.ContainingType, knownTypes): {
                var eventType = method.TypeArguments[0];

                if (IsConcreteEvent(eventType) && !HasEventTypeAttribute(eventType, knownTypes) && !IsExplicitlyRegistered(eventType, ctx, knownTypes)) {
                    ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, inv.Syntax.GetLocation(), eventType.ToDisplayString()));
                }

                return;
            }
        }

        // Case 2: Functional service: Act/ActAsync handlers
        if (method.Name is "Act" or "ActAsync") {
            // Heuristic: only consider the overloads that accept a delegate and are defined in CommandHandlerBuilder interfaces/classes
            if (!IsFunctionalServiceAct(method, knownTypes)) return;

            foreach (var value in inv.Arguments.Select(arg => arg.Value)) {
                switch (value) {
                    case null:
                        continue;
                    // If the argument is a lambda, analyze its body for created event instances
                    case IAnonymousFunctionOperation lambda:
                        AnalyzeDelegateBodyForEventCreations(ctx, lambda.Body, knownTypes);

                        break;
                    case IConversionOperation { Operand: IAnonymousFunctionOperation lambdaConv }:
                        AnalyzeDelegateBodyForEventCreations(ctx, lambdaConv.Body, knownTypes);

                        break;
                }
            }
        }
    }

    static void AnalyzeDelegateBodyForEventCreations(OperationAnalysisContext ctx, IBlockOperation? body, KnownTypeSymbols knownTypes) {
        if (body is null) return;

        foreach (var op in body.Descendants()) {
            if (op is IObjectCreationOperation create) {
                var created = create.Type;

                if (created != null && IsConcreteEvent(created) && !HasEventTypeAttribute(created, knownTypes) && !IsExplicitlyRegistered(created, ctx, knownTypes)) {
                    ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, create.Syntax.GetLocation(), created.ToDisplayString()));
                }
            }
        }
    }

    static void AnalyzeObjectCreation(OperationAnalysisContext ctx, KnownTypeSymbols knownTypes) {
        // Global safety net for method groups passed into Act where we couldn't traverse the body via the invocation site.
        // If the object creation is within a method that appears to be an Act handler (returns NewEvents/ IEnumerable<object>), warn.
        if (ctx.Operation is not IObjectCreationOperation create) return;

        var created = create.Type;

        if (created is null || !IsConcreteEvent(created)) return;

        var method = GetEnclosingMethod(ctx.Operation);

        if (method == null) return;

        if (ReturnsNewEvents(method)) {
            if (!HasEventTypeAttribute(created, knownTypes) && !IsExplicitlyRegistered(created, ctx, knownTypes)) {
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

    static bool IsAggregate(INamedTypeSymbol? type, KnownTypeSymbols knownTypes) {
        if (type == null) return false;

        // Walk base types to check if it derives from Eventuous.Aggregate<>
        for (var t = type; t != null; t = t.BaseType) {
            // Prefer symbol comparison (refactoring-safe)
            if (knownTypes.Aggregate != null) {
                if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, knownTypes.Aggregate)) {
                    return true;
                }
            }
            else {
                // Fallback to string comparison
                if (t is { Name: "Aggregate", Arity: 1 } && t.ContainingNamespace.ToDisplayString() == BaseNamespace) {
                    return true;
                }
            }
        }

        return false;
    }

    static bool IsState(INamedTypeSymbol? type, KnownTypeSymbols knownTypes) {
        if (type == null) return false;

        // Walk base types to check if it derives from Eventuous.State<>
        for (var t = type; t != null; t = t.BaseType) {
            // Prefer symbol comparison (refactoring-safe)
            if (knownTypes.State != null) {
                if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, knownTypes.State)) {
                    return true;
                }
            }
            else {
                // Fallback to string comparison
                if (t is { Name: "State", Arity: 1 } && t.ContainingNamespace.ToDisplayString() == BaseNamespace) {
                    return true;
                }
            }
        }

        return false;
    }

    static bool IsFunctionalServiceAct(IMethodSymbol method, KnownTypeSymbols knownTypes) {
        // We only care about the Act methods from CommandHandlerBuilder and the related interfaces in Eventuous namespace
        if (method.Name is not ("Act" or "ActAsync")) return false;

        var containing = method.ContainingType;

        if (containing == null) return false;

        // Prefer symbol comparison (refactoring-safe)
        if (knownTypes.CommandHandlerBuilder != null || knownTypes.IDefineExecution != null ||
            knownTypes.ICommandHandlerBuilder != null || knownTypes.IDefineStoreOrExecution != null) {
            return SymbolEqualityComparer.Default.Equals(containing, knownTypes.CommandHandlerBuilder) ||
                   SymbolEqualityComparer.Default.Equals(containing, knownTypes.IDefineExecution) ||
                   SymbolEqualityComparer.Default.Equals(containing, knownTypes.ICommandHandlerBuilder) ||
                   SymbolEqualityComparer.Default.Equals(containing, knownTypes.IDefineStoreOrExecution);
        }

        // Fallback to string comparison
        var ns = containing.ContainingNamespace?.ToDisplayString();
        if (ns != BaseNamespace) return false;

        return containing.Name is "CommandHandlerBuilder" or "IDefineExecution" or "ICommandHandlerBuilder" or "IDefineStoreOrExecution";
    }

    static bool IsConcreteEvent(ITypeSymbol type) => type.TypeKind is TypeKind.Class or TypeKind.Struct;

    static bool HasEventTypeAttribute(ITypeSymbol type, KnownTypeSymbols knownTypes) {
        // Prefer symbol comparison (refactoring-safe)
        if (knownTypes.EventTypeAttribute != null) {
            return type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, knownTypes.EventTypeAttribute));
        }

        // Fallback to string comparison
        return (from attrClass in type.GetAttributes().Select(a => a.AttributeClass).OfType<INamedTypeSymbol>()
                let name = attrClass.ToDisplayString()
                where name == EventTypeAttrFqcn || attrClass.Name is EventTypeAttribute
                select attrClass).Any();
    }
}
