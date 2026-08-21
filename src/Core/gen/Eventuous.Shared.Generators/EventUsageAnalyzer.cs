// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    /// Symbol comparison against these is the only matching mechanism; if a symbol doesn't resolve,
    /// the corresponding check simply doesn't apply. The metadata names in <see cref="WellKnownTypeNames"/>
    /// are pinned by tests against the real Eventuous assemblies.
    /// </summary>
    sealed class KnownTypeSymbols(Compilation compilation) {
        public INamedTypeSymbol? EventTypeAttribute      { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.EventTypeAttribute);
        public INamedTypeSymbol? TypeMapper              { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.TypeMapper);
        public INamedTypeSymbol? Aggregate               { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.Aggregate);
        public INamedTypeSymbol? State                   { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.State);
        public INamedTypeSymbol? CommandHandlerBuilder   { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.CommandHandlerBuilder);
        public INamedTypeSymbol? IDefineExecution        { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.IDefineExecution);
        public INamedTypeSymbol? ICommandHandlerBuilder  { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.ICommandHandlerBuilder);
        public INamedTypeSymbol? IDefineStoreOrExecution { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.IDefineStoreOrExecution);
        public INamedTypeSymbol? BaseEventHandler        { get; } = GetBestTypeByMetadataName(compilation, WellKnownTypeNames.BaseEventHandler);

        // GetTypeByMetadataName returns null not only when the type is missing but also when more than
        // one referenced assembly defines it; in the ambiguous case pick the single accessible candidate
        static INamedTypeSymbol? GetBestTypeByMetadataName(Compilation compilation, string metadataName) {
            var type = compilation.GetTypeByMetadataName(metadataName);

            if (type != null) return type;

            INamedTypeSymbol? best = null;

            foreach (var candidate in compilation.GetTypesByMetadataName(metadataName)) {
                if (candidate.DeclaredAccessibility != Accessibility.Public
                 && !SymbolEqualityComparer.Default.Equals(candidate.ContainingAssembly, compilation.Assembly)) continue;

                if (best != null) return null;

                best = candidate;
            }

            return best;
        }
    }

    static ImmutableHashSet<ITypeSymbol> GetExplicitRegistrations(OperationAnalysisContext ctx, KnownTypeSymbols knownTypes) {
        var model = ctx.Operation.SemanticModel;
        if (model == null || knownTypes.TypeMapper == null) return ImmutableHashSet<ITypeSymbol>.Empty;
        var root = ctx.Operation.Syntax.SyntaxTree.GetRoot();
        var set = ImmutableHashSet.CreateBuilder<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var invSyntax in root.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
            if (model.GetOperation(invSyntax) is not IInvocationOperation op) continue;
            var m = op.TargetMethod;

            if (m.Name != "AddType") continue;
            if (!SymbolEqualityComparer.Default.Equals(m.ContainingType, knownTypes.TypeMapper)) continue;

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
            case { Name: "On", TypeArguments.Length: 1 } when IsState(method.ContainingType, knownTypes):
            // Case 1d: EventHandler.On<T>(...) handler registrations
            case { Name: "On", TypeArguments.Length: 1 } when IsEventHandler(method.ContainingType, knownTypes): {
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

            // If the argument is a lambda, analyze its body for created event instances.
            // Lambdas passed as delegate arguments surface as IDelegateCreationOperation, possibly wrapped in a conversion.
            foreach (var value in inv.Arguments.Select(arg => arg.Value)) {
                var lambda = value switch {
                    IAnonymousFunctionOperation anon                                                              => anon,
                    IDelegateCreationOperation { Target: IAnonymousFunctionOperation anon }                       => anon,
                    IConversionOperation { Operand: IAnonymousFunctionOperation anon }                            => anon,
                    IConversionOperation { Operand: IDelegateCreationOperation { Target: IAnonymousFunctionOperation anon } } => anon,
                    _                                                                                             => null
                };

                if (lambda != null) AnalyzeDelegateBodyForEventCreations(ctx, lambda.Body, knownTypes);
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

        // Creations inside lambdas passed to Act/ActAsync are reported by the invocation traversal; skip them here
        if (ReturnsNewEvents(method) && !IsWithinFunctionalActInvocation(create, knownTypes)) {
            if (!HasEventTypeAttribute(created, knownTypes) && !IsExplicitlyRegistered(created, ctx, knownTypes)) {
                ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, create.Syntax.GetLocation(), created.ToDisplayString()));
            }
        }
    }

    static bool IsWithinFunctionalActInvocation(IOperation op, KnownTypeSymbols knownTypes) {
        for (var p = op.Parent; p != null; p = p.Parent) {
            if (p is IInvocationOperation inv && IsFunctionalServiceAct(inv.TargetMethod, knownTypes)) return true;
        }

        return false;
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

    // Walk base types to check if the type derives from Eventuous.Aggregate<>
    static bool IsAggregate(INamedTypeSymbol? type, KnownTypeSymbols knownTypes) => DerivesFrom(type, knownTypes.Aggregate);

    // Walk base types to check if the type derives from Eventuous.State<>
    static bool IsState(INamedTypeSymbol? type, KnownTypeSymbols knownTypes) => DerivesFrom(type, knownTypes.State);

    static bool IsEventHandler(INamedTypeSymbol? type, KnownTypeSymbols knownTypes) => DerivesFrom(type, knownTypes.BaseEventHandler);

    static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol? baseDefinition) {
        if (baseDefinition == null) return false;

        for (var t = type; t != null; t = t.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, baseDefinition)) return true;
        }

        return false;
    }

    static bool IsFunctionalServiceAct(IMethodSymbol method, KnownTypeSymbols knownTypes) {
        // We only care about the Act methods from CommandHandlerBuilder and the related interfaces in Eventuous namespace.
        // The containing type at the call site is a constructed generic, so compare its original definition.
        if (method.Name is not ("Act" or "ActAsync")) return false;

        var definition = method.ContainingType?.OriginalDefinition;

        if (definition == null) return false;

        return SymbolEqualityComparer.Default.Equals(definition, knownTypes.CommandHandlerBuilder)
            || SymbolEqualityComparer.Default.Equals(definition, knownTypes.IDefineExecution)
            || SymbolEqualityComparer.Default.Equals(definition, knownTypes.ICommandHandlerBuilder)
            || SymbolEqualityComparer.Default.Equals(definition, knownTypes.IDefineStoreOrExecution);
    }

    // System.Object is excluded: an object-typed value (e.g. StreamEvent.Payload or IMessageConsumeContext.Message)
    // carries a runtime-resolved event type, so there is nothing to annotate at the call site.
    // The System namespace is excluded as a whole: framework types constructed inside handlers
    // (List<object>, DateTime, ...) are never domain events.
    static bool IsConcreteEvent(ITypeSymbol type)
        => type.SpecialType is not SpecialType.System_Object
        && type.TypeKind is TypeKind.Class or TypeKind.Struct
        && !IsInSystemNamespace(type);

    static bool IsInSystemNamespace(ITypeSymbol type) {
        for (var ns = type.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace) {
            if (ns.ContainingNamespace is { IsGlobalNamespace: true }) return ns.Name == "System";
        }

        return false;
    }

    static bool HasEventTypeAttribute(ITypeSymbol type, KnownTypeSymbols knownTypes)
        => knownTypes.EventTypeAttribute != null
        && type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, knownTypes.EventTypeAttribute));
}
