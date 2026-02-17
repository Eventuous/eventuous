// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Eventuous.Subscriptions.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class ConsumeContextConverterGenerator : IIncrementalGenerator {
    const string InterfaceNamespace = "Eventuous.Subscriptions.Context";
    const string InterfaceName      = "IMessageConsumeContext";
    const string InterfaceFqn       = $"{InterfaceNamespace}.{InterfaceName}`1";

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Resolve the IMessageConsumeContext<> symbol from the compilation
        var messageConsumeContextSymbol = context.CompilationProvider
            .Select(static (c, _) => c.GetTypeByMetadataName(InterfaceFqn));

        var candidateTypes = context.SyntaxProvider
            .CreateSyntaxProvider(IsPotentialUsage, Transform)
            .Where(static t => t is not null)
            .Combine(messageConsumeContextSymbol)
            .Select(static (pair, _) => TransformWithSymbol(pair.Left, pair.Right))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!)
            .Collect();

        context.RegisterSourceOutput(candidateTypes, Generate);
    }

    static bool IsPotentialUsage(SyntaxNode node, CancellationToken _) {
        return node switch {
            GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } g => g.Identifier.Text is "IMessageConsumeContext" or "On",
            // handle qualified names like Eventuous.Subscriptions.Context.IMessageConsumeContext<T>
            QualifiedNameSyntax { Right: GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } g2 } => g2.Identifier.Text == "IMessageConsumeContext",
            // implicit: lambdas where parameter type is inferred to IMessageConsumeContext<T>
            SimpleLambdaExpressionSyntax => true,
            ParenthesizedLambdaExpressionSyntax => true,
            _ => false
        };
    }

    static GeneratorSyntaxContext? Transform(GeneratorSyntaxContext ctx, CancellationToken _) {
        // Just return the context for further processing
        return ctx;
    }

    static string? TransformWithSymbol(GeneratorSyntaxContext? ctx, INamedTypeSymbol? messageConsumeContextSymbol) {
        if (ctx is not { } context) return null;

        // Explicit generic type usage: IMessageConsumeContext<T>
        if (context.Node is GenericNameSyntax g) {
            // Case 1: explicit IMessageConsumeContext<T>
            var symbol = context.SemanticModel.GetSymbolInfo(g).Symbol as INamedTypeSymbol
                         ?? context.SemanticModel.GetTypeInfo(g).Type as INamedTypeSymbol;

            if (symbol != null) {
                var def = symbol.OriginalDefinition;
                if (IsTargetInterface(def, messageConsumeContextSymbol) && symbol.TypeArguments.Length == 1) {
                    var arg = symbol.TypeArguments[0];
                    return GetTypeSyntax(arg);
                }
            }

            // Case 2: On<TSomething>(...) where generic parameter name indicates an Event (e.g., TEvent, TIntegrationEvent)
            if (g.Identifier.Text == "On" && g.TypeArgumentList.Arguments.Count == 1) {
                // Try to get T from the generic method symbol On<T>(...)
                var inv = g.Parent as InvocationExpressionSyntax ?? g.Parent?.Parent as InvocationExpressionSyntax;
                if (inv != null) {
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(inv).Symbol;
                    var method = symbolInfo as IMethodSymbol;
                    if (method?.TypeArguments.Length == 1 && ShouldTreatGenericOnAsEvent(method)) {
                        var tArg = method.TypeArguments[0];
                        if (tArg.IsReferenceType) return GetTypeSyntax(tArg);
                    }
                }
                // If we cannot resolve the method symbol reliably, skip to avoid false positives
            }
        }

        // Qualified explicit usage: Namespace.IMessageConsumeContext<T>
        if (context.Node is QualifiedNameSyntax { Right: GenericNameSyntax g2 }) {
            var symbol = context.SemanticModel.GetSymbolInfo(g2).Symbol as INamedTypeSymbol
                         ?? context.SemanticModel.GetTypeInfo(g2).Type as INamedTypeSymbol;

            if (symbol != null) {
                var def = symbol.OriginalDefinition;
                if (IsTargetInterface(def, messageConsumeContextSymbol) && symbol.TypeArguments.Length == 1) {
                    var arg = symbol.TypeArguments[0];
                    return GetTypeSyntax(arg);
                }
            }
        }

        // Implicit usage via lambda parameter type inference
        if (context.Node is LambdaExpressionSyntax lambda) {
            var typeInfo = context.SemanticModel.GetTypeInfo(lambda);
            var delegateType = typeInfo.ConvertedType as INamedTypeSymbol;
            var invoke = delegateType?.DelegateInvokeMethod;
            if (invoke is not null) {
                foreach (var p in invoke.Parameters) {
                    if (TryExtractTypeArgFromIMessageConsumeContext(p.Type, messageConsumeContextSymbol, out var typeArg)) {
                        return GetTypeSyntax(typeArg);
                    }
                }
            }
        }

        return null;
    }

    static string GetTypeSyntax(ITypeSymbol symbol) {
        // Use fully qualified name with global:: prefix
        var name = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return name.StartsWith("global::", StringComparison.Ordinal) ? name : $"global::{name}";
    }

    static bool IsTargetInterface(INamedTypeSymbol def, INamedTypeSymbol? messageConsumeContextSymbol) {
        // Prefer symbol comparison (refactoring-safe)
        if (messageConsumeContextSymbol is not null) {
            return SymbolEqualityComparer.Default.Equals(def, messageConsumeContextSymbol);
        }

        // Fallback to string-based comparison
        return def is { Arity: 1, Name: InterfaceName } &&
               def.ContainingNamespace?.ToDisplayString() == InterfaceNamespace;
    }

    static bool ShouldTreatGenericOnAsEvent(IMethodSymbol method) {
        if (method is not { Name: "On" }) return false;
        var def = method.OriginalDefinition;
        if (def.TypeParameters.Length != 1) return false;
        var paramName = def.TypeParameters[0].Name;
        // Heuristic: only treat as event when the generic parameter name indicates an Event
        // e.g., TEvent, TIntegrationEvent, etc. Skip TCommand, T, etc.
        return paramName.IndexOf("Event", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool TryExtractTypeArgFromIMessageConsumeContext(
        ITypeSymbol type,
        INamedTypeSymbol? messageConsumeContextSymbol,
        out ITypeSymbol typeArg) {
        if (type is INamedTypeSymbol named) {
            var def = named.OriginalDefinition;
            if (IsTargetInterface(def, messageConsumeContextSymbol) && named.TypeArguments.Length == 1) {
                typeArg = named.TypeArguments[0];
                return true;
            }
        }
        typeArg = null!;
        return false;
    }

    static void Generate(SourceProductionContext context, ImmutableArray<string> typeNames) {
        var distinct = typeNames.Where(static t => !string.IsNullOrWhiteSpace(t)).Distinct().ToArray();

        if (distinct.Length == 0) {
            // Always emit a marker file so users can verify the generator ran
            const string marker = "// <auto-generated> Generator ran, but found no IMessageConsumeContext<T> usages in this compilation. </auto-generated>\n";
            context.AddSource("MessageConsumeContext_Converters.Info.g.cs", marker);
            return;
        }

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Eventuous.Subscriptions.Context;");
        sb.AppendLine("using Eventuous.Subscriptions.Consumers;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Eventuous.Subscriptions.Consumers;");
        sb.AppendLine();
        sb.AppendLine("internal static class MessageConsumeContextGeneratedConvertersInitializer {");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize() => MessageConsumeContextConverter.Register(Convert);");
        sb.AppendLine();
        sb.AppendLine("    private static IMessageConsumeContext? Convert(IMessageConsumeContext context)");
        sb.AppendLine("        => context.Message switch {");

        foreach (var t in distinct) {
            sb.AppendLine($"            {t} => new MessageConsumeContext<{t}>(context),");
        }

        sb.AppendLine("            _ => null");
        sb.AppendLine("        };");
        sb.AppendLine("}");

        context.AddSource("MessageConsumeContext_Converters.g.cs", sb.ToString());
    }
}
