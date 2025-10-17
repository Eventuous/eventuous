// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable CognitiveComplexity

namespace Eventuous.Extensions.AspNetCore.Generators;

[Generator(LanguageNames.CSharp)]
public class HttpCommandMappingGenerator : IIncrementalGenerator {
    const string ExtensionsNamespace      = "Eventuous.Extensions.AspNetCore";
    const string HttpExtensionsNamespace  = $"{ExtensionsNamespace}.Http";
    const string HttpCommandAttributeType = "HttpCommandAttribute";
    const string HttpCommandAttributeFqn  = $"{HttpExtensionsNamespace}.{HttpCommandAttributeType}";
    const string HttpCommandsAttributeFqn = $"{HttpExtensionsNamespace}.HttpCommandsAttribute";
    const string StateBaseFqn             = "Eventuous.State`1";
    const string CommandServiceFqn        = "Eventuous.ICommandService`1";
    const string CommandServiceType       = "ICommandService";

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var syntaxProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } or RecordDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx,  _) => GetCandidate(ctx)
            )
            .Where(static m => m.HasValue)
            .Select(static (m, _) => m!.Value);

        var stateTypes = context.CompilationProvider.Select(static (compilation, _) => DiscoverStateTypes(compilation));

        var combined = syntaxProvider.Collect().Combine(stateTypes);

        context.RegisterSourceOutput(combined, static (spc, data) => Execute(spc, data.Left));
    }

    static Candidate? GetCandidate(GeneratorSyntaxContext context) {
        if (context.Node is not CSharpSyntaxNode node) return null;

        var model = context.SemanticModel;

        var symbol = node switch {
            ClassDeclarationSyntax cds  => model.GetDeclaredSymbol(cds),
            RecordDeclarationSyntax rds => model.GetDeclaredSymbol(rds),
            _                           => null
        };

        // check if a class has HttpCommandAttribute
        var httpCmdAttr = symbol?.GetAttributes().FirstOrDefault(a => SymbolEqualsFqn(a.AttributeClass, HttpCommandAttributeFqn));

        if (symbol == null || httpCmdAttr == null) return null;

        // parent attr (optional)
        var parent = symbol.ContainingType;

        var httpCmdsAttr = parent?
                .GetAttributes()
                .FirstOrDefault(a => SymbolEqualsFqn(a.AttributeClass, HttpCommandsAttributeFqn))
         ?? parent?.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass is { Name: HttpCommandAttributeType } ats &&
                    ats.ContainingNamespace?.ToDisplayString() == HttpExtensionsNamespace
                );

        return new Candidate(symbol, httpCmdAttr, httpCmdsAttr);
    }

    static ImmutableArray<INamedTypeSymbol> DiscoverStateTypes(Compilation compilation) {
        var list = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var tree in compilation.SyntaxTrees) {
            var model = compilation.GetSemanticModel(tree);
            var root  = tree.GetRoot();

            // 1) Classes deriving from State<T>
            foreach (var node in root.DescendantNodes().OfType<ClassDeclarationSyntax>()) {
                if (model.GetDeclaredSymbol(node) is { } type) {
                    if (DerivesFromState(type)) {
                        list.Add(type);
                    }
                }
            }

            // 2) Generic usages of ICommandService<TState>
            foreach (var g in root.DescendantNodes().OfType<GenericNameSyntax>()) {
                if (g.Identifier.ValueText != CommandServiceType || g.TypeArgumentList.Arguments.Count != 1) {
                    continue;
                }

                var symbol = model.GetSymbolInfo(g).Symbol;

                if (symbol is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == CommandServiceFqn) {
                    var stateArgSyntax = g.TypeArgumentList.Arguments[0];

                    if (model.GetTypeInfo(stateArgSyntax).Type is INamedTypeSymbol stateType) {
                        list.Add(stateType);
                    }
                }
            }
        }

        return list.ToImmutable();

        bool DerivesFromState(INamedTypeSymbol t) {
            for (var bt = t.BaseType; bt != null; bt = bt.BaseType) {
                if (bt.OriginalDefinition.ToDisplayString() == StateBaseFqn) return true;
            }

            return false;
        }
    }

    static void Execute(SourceProductionContext context, ImmutableArray<Candidate> candidates) {
        if (candidates.IsDefaultOrEmpty) return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> generated by Eventuous.Extensions.AspNetCore.Generators");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Microsoft.AspNetCore.Routing;");
        sb.AppendLine($"using {HttpExtensionsNamespace};");
        sb.AppendLine("namespace Eventuous.Extensions.AspNetCore.Generated;");
        sb.AppendLine("public static class __Eventuous_HttpCommand_Module {");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    public static void Init() {");

        List<(string Route, Location Location)> routes = [];

        foreach (var cand in candidates) {
            var cmdType = cand.CommandType;
            var cmdFqn  = cmdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var (route, policy, state) = ExtractArgs(context, cand);

            if (!string.IsNullOrEmpty(route)) {
                routes.Add((route, cmdType.Locations[0]));
            }

            if (state is not null) {
                var stateFqn = state.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var mapString = $"static builder => builder.MapCommand<{cmdFqn}, {stateFqn}>(\"{Escape(route)}\", null, {(policy is null ? "null" : $"\"{Escape(policy)}\"")})";

                // Register for a specific state
                sb.AppendLine(
                    $"""
                             CommandMappingRegistry.RegisterForState(
                                 typeof({stateFqn}),
                                 typeof({cmdFqn}),
                                 {mapString}
                             );
                     """
                );

                // Also for non-generic discovery
                sb.AppendLine(
                    $"""
                              CommandMappingRegistry.RegisterAll(
                                  typeof({cmdFqn}),
                                  {mapString}
                              );
                      """
                );
            }
            else {
                // No explicit state: defer binding to runtime generic MapDiscoveredCommands<TState>
                sb.AppendLine(
                    $"""
                             CommandMappingRegistry.RegisterWithoutState(
                                  typeof({cmdFqn}),
                                  "{Escape(route)}", {(policy is null ? "null" : $"\"{Escape(policy)}\"")}
                             );
                     """
                );
            }
        }

        sb.AppendLine("    }");

        List<CandidateInfo> cmdInfos = [];

        foreach (var cand in candidates) {
            var cmdType = cand.CommandType;
            var cmdFqn  = cmdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var (route, policy, state) = ExtractArgs(context, cand);

            if (state is not null) {
                var stateFqn  = state.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var stateName = state.Name;
                cmdInfos.Add(new(cmdFqn, stateFqn, stateName, route, policy, cand.CommandType.Locations[0]));
            }
        }

        foreach (var g in cmdInfos.GroupBy(x => x.State)) {
            var name = g.Key.Replace("State", "").Replace("Aggregate", "");
            sb.AppendLine($"    public static IEndpointRouteBuilder Map{name}Commands(this IEndpointRouteBuilder builder) {{");

            foreach (var info in g) {
                sb.AppendLine($"        builder.MapCommand<{info.CmdFqn}, {info.StateFqn}>(\"{Escape(info.Route)}\", null, {(info.Policy is null ? "null" : $"\"{Escape(info.Policy)}\"")});");
            }

            sb.AppendLine("        return builder;");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        context.AddSource("Eventuous_HttpCommand_Module.g.cs", sb.ToString());

        if (routes.Count > 0) {
            var nonDistinctValues = routes
                .GroupBy(s => s.Route)
                .Where(g => g.Count() > 1)
                .Select(g => (g.Key, Locations: g.Select(x => x.Location).ToArray()))
                .ToList();

            foreach (var duplicate in nonDistinctValues) {
                foreach (var location in duplicate.Locations) {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicateRouteDiagnostic, location, duplicate.Key));
                }
            }
        }
    }

    record struct CandidateInfo(string CmdFqn, string StateFqn, string State, string Route, string? Policy, Location Location);

    static (string route, string? policy, INamedTypeSymbol? state) ExtractArgs(SourceProductionContext spc, Candidate c) {
        var route  = GetStringArg(c.HttpCommandAttr, "Route") ?? GetDefaultRoute(c.CommandType);
        var policy = GetStringArg(c.HttpCommandAttr, "PolicyName");
        var state  = GetTypeArg(c.HttpCommandAttr, "StateType") ?? GetParentState(c.ParentCommandsAttr);
        // validate mismatch between parent and attr if both provided
        var parentState = GetParentState(c.ParentCommandsAttr);

        if (parentState != null) {
            var own = GetTypeArg(c.HttpCommandAttr, "StateType");

            if (own != null && !SymbolEqualityComparer.Default.Equals(own, parentState)) {
                // report diagnostic
                var diag = Diagnostic.Create(
                    Diagnostics.ParentStateMatchDiagnostic,
                    c.CommandType.Locations.FirstOrDefault(),
                    c.CommandType.Name,
                    own.ToDisplayString(),
                    parentState.ToDisplayString()
                );
                spc.ReportDiagnostic(diag);
            }

            state = parentState;
        }

        return (route, policy, state);
    }

    static string GetDefaultRoute(INamedTypeSymbol cmd) {
        var name = cmd.Name;

        if (string.IsNullOrEmpty(name)) return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    static string? GetStringArg(AttributeData attr, string name) {
        foreach (var kv in attr.NamedArguments) {
            if (kv.Key == name && kv.Value.Value is string s) return s;
        }

        return null;
    }

    static INamedTypeSymbol? GetTypeArg(AttributeData attr, string name) {
        // First, look for a named argument like StateType = typeof(T)
        foreach (var kv in attr.NamedArguments) {
            if (kv.Key == name && kv.Value.Value is INamedTypeSymbol t) return t;
        }

        // If not provided explicitly, try to infer from a generic attribute usage like [HttpCommand<TState>]
        if (attr.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 } named) {
            var ta = named.TypeArguments[0];

            if (ta is INamedTypeSymbol nt) return nt;
        }

        return null;
    }

    static INamedTypeSymbol? GetParentState(AttributeData? parent) {
        if (parent == null) return null;

        // 1) If it's a generic attribute like [HttpCommands<TState>], take the first type argument
        if (parent.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 } named) {
            var ta = named.TypeArguments[0];

            if (ta is INamedTypeSymbol nt) return nt;
        }

        // 2) Otherwise try constructor arguments (for non-generic attribute overloads)
        foreach (var ctorArg in parent.ConstructorArguments) {
            if (ctorArg.Value is INamedTypeSymbol t) return t;
        }

        // 3) Or a named argument like StateType = typeof(T)
        foreach (var kv in parent.NamedArguments) {
            if (kv is { Key: "StateType", Value.Value: INamedTypeSymbol t }) return t;
        }

        return null;
    }

    static bool SymbolEqualsFqn(ISymbol? symbol, string fqn) {
        if (symbol == null) return false;

        // Exact match first
        if (symbol.ToDisplayString() == fqn) return true;

        // Compare by namespace + simple name (ignores generic arity and type arguments)
        if (symbol is INamedTypeSymbol nts) {
            static string GetNsName(INamedTypeSymbol s) {
                var ns = s.ContainingNamespace?.ToDisplayString();

                return string.IsNullOrEmpty(ns) ? s.Name : ns + "." + s.Name;
            }

            var simple = GetNsName(nts);

            if (simple == fqn) return true;

            // Compare original definition as well to handle constructed generics
            var od = nts.OriginalDefinition;

            if (GetNsName(od) == fqn) return true;
        }

        return false;
    }

    static string Escape(string s) => s.Replace("\\", @"\\").Replace("\"", "\\\"");

    readonly struct Candidate(INamedTypeSymbol commandType, AttributeData httpCommandAttr, AttributeData? parentCommandsAttr) {
        public readonly INamedTypeSymbol CommandType        = commandType;
        public readonly AttributeData    HttpCommandAttr    = httpCommandAttr;
        public readonly AttributeData?   ParentCommandsAttr = parentCommandsAttr;
    }
}
