// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Eventuous.Extensions.AspNetCore.Generators.Diagnostics;

// ReSharper disable CognitiveComplexity

namespace Eventuous.Extensions.AspNetCore.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HttpCommandStateMismatchAnalyzer : DiagnosticAnalyzer {

    const string NamespaceName       = "Eventuous.Extensions.AspNetCore.Http";
    const string BuilderTypeName     = "CommandServiceRouteBuilder";
    const string RouteBuilderExtName = "RouteBuilderExtensions";
    const string AttribTypeName      = "HttpCommandAttribute";
    const string StateTypeParamName  = "StateType";
    const string RouteParamName      = "Route";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [StateMatchRule, RouteRule];

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the invoked method symbol

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol symbol) return;

        // We care about MapCommand invocations only
        if (symbol.Name != "MapCommand") return;

        // Case A: Fluent MapCommand<TContract, TCommand> on CommandServiceRouteBuilder<TState>
        if (symbol is { IsExtensionMethod: false }) {
            // Prefer determining TState from the receiver expression type, which is more robust
            if (invocation.Expression is MemberAccessExpressionSyntax { Expression: { } recvExpr, Name: GenericNameSyntax { TypeArgumentList.Arguments.Count: 2 } gname1 }) {
                if (context.SemanticModel.GetTypeInfo(recvExpr, context.CancellationToken).Type is INamedTypeSymbol { Name: BuilderTypeName, Arity: 1 } recvType &&
                    recvType.ContainingNamespace.ToDisplayString() == NamespaceName) {
                    var tState = recvType.TypeArguments[0];

                    var tContractTypeSyntax = gname1.TypeArgumentList.Arguments[0];
                    var tContract           = context.SemanticModel.GetTypeInfo(tContractTypeSyntax, context.CancellationToken).Type;

                    if (tContract != null) {
                        // EVTA001 - state mismatch
                        var attrState = GetHttpCommandStateTypeArg(tContract);

                        if (attrState != null && !SymbolEqualityComparer.Default.Equals(attrState, tState)) {
                            ReportState(context, invocation, tContract, attrState, tState);
                        }

                        // EVTA002 - route override mismatch for builder MapCommand<TContract,TCommand>(string? route, ...)
                        // Only if there is an explicit route argument in this invocation
                        var routeArgValue = GetFirstStringArgumentConstant(invocation, context);
                        var attrRoute     = GetHttpCommandRouteConstant(tContract);

                        if (!string.IsNullOrEmpty(attrRoute) && routeArgValue != null && routeArgValue != attrRoute) {
                            ReportRoute(context, invocation, tContract, attrRoute!, routeArgValue);
                        }
                    }
                }
            }
            else if (symbol is { ContainingType: not null } || symbol.ContainingType is not null) {
                // Fallback to previous logic using method symbol's containing type (in case receiver type retrieval fails)
                var containingType = symbol.ContainingType;

                if (containingType is { Name: BuilderTypeName, Arity: 1 }
                 && containingType.ContainingNamespace.ToDisplayString() == NamespaceName
                 && invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax { TypeArgumentList.Arguments.Count: 2 } gname }) {
                    var tState              = containingType.TypeArguments.FirstOrDefault();
                    var tContractTypeSyntax = gname.TypeArgumentList.Arguments[0];
                    var tContract           = context.SemanticModel.GetTypeInfo(tContractTypeSyntax, context.CancellationToken).Type;

                    if (tContract != null && tState != null) {
                        var attrState = GetHttpCommandStateTypeArg(tContract);

                        if (attrState != null && !SymbolEqualityComparer.Default.Equals(attrState, tState)) {
                            ReportState(context, invocation, tContract, attrState, tState);
                        }

                        var routeArgValue = GetFirstStringArgumentConstant(invocation, context);
                        var attrRoute     = GetHttpCommandRouteConstant(tContract);

                        if (!string.IsNullOrEmpty(attrRoute) && routeArgValue != null && routeArgValue != attrRoute) {
                            ReportRoute(context, invocation, tContract, attrRoute!, routeArgValue);
                        }
                    }
                }
            }
        }

        // Case B: Extension MapCommand<TContract, TCommand, TState>(this IEndpointRouteBuilder, ...)
        if (symbol is { IsExtensionMethod: true, ReducedFrom: not null }) {
            var reducedFrom = symbol.ReducedFrom;

            if (reducedFrom.ContainingType.Name                   == RouteBuilderExtName
             && reducedFrom.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Routing"
             && reducedFrom.TypeParameters.Length                 == 3) {
                // Need to read generic arguments from the syntax if present, otherwise from the constructed method symbol
                INamedTypeSymbol? tContract = null;
                ITypeSymbol?      tState    = null;

                if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax } maes2) {
                    var gname2 = (GenericNameSyntax)maes2.Name;

                    if (gname2.TypeArgumentList.Arguments.Count == 3) {
                        tContract = context.SemanticModel.GetTypeInfo(gname2.TypeArgumentList.Arguments[0], context.CancellationToken).Type as INamedTypeSymbol;
                        tState    = context.SemanticModel.GetTypeInfo(gname2.TypeArgumentList.Arguments[2], context.CancellationToken).Type;
                    }
                }

                if (tContract == null || tState == null) {
                    if (symbol.TypeArguments.Length == 3) {
                        tContract = symbol.TypeArguments[0] as INamedTypeSymbol;
                        tState    = symbol.TypeArguments[2];
                    }
                }

                if (tContract != null && tState != null) {
                    // EVTA001
                    var attrState = GetHttpCommandStateTypeArg(tContract);

                    if (attrState != null && !SymbolEqualityComparer.Default.Equals(attrState, tState)) {
                        ReportState(context, invocation, tContract, attrState, tState);
                    }

                    // EVTA002 - check route override for extension overload with (string? route, ...)
                    var routeArgValue = GetFirstStringArgumentConstant(invocation, context);
                    var attrRoute     = GetHttpCommandRouteConstant(tContract);

                    if (!string.IsNullOrEmpty(attrRoute) && routeArgValue != null && routeArgValue != attrRoute) {
                        ReportRoute(context, invocation, tContract, attrRoute!, routeArgValue);
                    }
                }
            }
        }
    }

    static INamedTypeSymbol? GetHttpCommandStateTypeArg(ITypeSymbol contractType) {
        foreach (var attr in contractType.GetAttributes()) {
            var attrClass = attr.AttributeClass;

            if (attrClass == null) continue;

            if (attrClass.Name == AttribTypeName && attrClass.ContainingNamespace.ToDisplayString() == "Eventuous.Extensions.AspNetCore.Http") {
                // Prefer generic version
                if (attrClass is { IsGenericType: true, TypeArguments.Length: 1 }) {
                    return attrClass.TypeArguments[0] as INamedTypeSymbol;
                }

                // Try named argument StateType on non-generic attribute
                foreach (var arg in attr.NamedArguments) {
                    if (arg is { Key: StateTypeParamName, Value.Value: ITypeSymbol }) {
                        return arg.Value.Value as INamedTypeSymbol;
                    }
                }
            }
        }

        return null;
    }

    static string? GetHttpCommandRouteConstant(ITypeSymbol contractType) {
        foreach (var attr in contractType.GetAttributes()) {
            var attrClass = attr.AttributeClass;

            if (attrClass == null) continue;

            if (attrClass.Name == AttribTypeName && attrClass.ContainingNamespace.ToDisplayString() == "Eventuous.Extensions.AspNetCore.Http") {
                foreach (var arg in attr.NamedArguments) {
                    if (arg is { Key: RouteParamName, Value.Value: string s }) return s;
                }
            }
        }

        return null;
    }

    static string? GetFirstStringArgumentConstant(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context) {
        if (invocation.ArgumentList.Arguments.Count == 0) return null;

        var expr     = invocation.ArgumentList.Arguments[0].Expression;
        var constVal = context.SemanticModel.GetConstantValue(expr, context.CancellationToken);

        return constVal is { HasValue: true, Value: string s } ? s : null;
    }

    static void ReportState(SyntaxNodeAnalysisContext ctx, InvocationExpressionSyntax node, ITypeSymbol contract, ITypeSymbol attrState, ITypeSymbol builderState) {
        var diag = Diagnostic.Create(
            StateMatchRule,
            node.GetLocation(),
            contract.Name,
            attrState.Name,
            builderState.Name
        );
        ctx.ReportDiagnostic(diag);
    }

    static void ReportRoute(SyntaxNodeAnalysisContext ctx, InvocationExpressionSyntax node, ITypeSymbol contract, string attrRoute, string overrideRoute) {
        var diag = Diagnostic.Create(
            RouteRule,
            node.GetLocation(),
            contract.Name,
            attrRoute,
            overrideRoute
        );
        ctx.ReportDiagnostic(diag);
    }
}
