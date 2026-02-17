// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Eventuous.Spyglass.Generators.Constants;

namespace Eventuous.Spyglass.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class SpyglassGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var data = context.CompilationProvider.Select(static (compilation, _) => {
                var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
                var aggregates   = DiscoverAggregates(compilation);
                var states       = DiscoverStandaloneStates(compilation, aggregates);

                return (assemblyName, aggregates, states);
            }
        );

        context.RegisterSourceOutput(data, static (spc, d) => Generate(spc, d.assemblyName, d.aggregates, d.states));
    }

    static ImmutableArray<AggregateCandidate> DiscoverAggregates(Compilation compilation) {
        var builder = ImmutableArray.CreateBuilder<AggregateCandidate>();

        ProcessNamespace(compilation.Assembly.GlobalNamespace, builder);

        foreach (var ra in compilation.SourceModule.ReferencedAssemblySymbols) {
            ProcessNamespace(ra.GlobalNamespace, builder);
        }

        return builder.ToImmutable();
    }

    static ImmutableArray<StateCandidate> DiscoverStandaloneStates(
            Compilation                        compilation,
            ImmutableArray<AggregateCandidate> aggregates
        ) {
        var aggregateStateFqns = new HashSet<string>(aggregates.Select(a => a.StateFqn));
        var builder            = ImmutableArray.CreateBuilder<StateCandidate>();

        ProcessNamespaceForStates(compilation.Assembly.GlobalNamespace, builder, aggregateStateFqns);

        foreach (var ra in compilation.SourceModule.ReferencedAssemblySymbols) {
            ProcessNamespaceForStates(ra.GlobalNamespace, builder, aggregateStateFqns);
        }

        return builder.ToImmutable();
    }

    static void ProcessNamespaceForStates(INamespaceSymbol ns, ImmutableArray<StateCandidate>.Builder builder, HashSet<string> excludeFqns) {
        foreach (var member in ns.GetMembers()) {
            switch (member) {
                case INamespaceSymbol child:
                    ProcessNamespaceForStates(child, builder, excludeFqns);

                    break;
                case INamedTypeSymbol type:
                    ProcessTypeForState(type, builder, excludeFqns);

                    break;
            }
        }
    }

    static void ProcessTypeForState(INamedTypeSymbol type, ImmutableArray<StateCandidate>.Builder builder, HashSet<string> excludeFqns) {
        if (!type.IsAbstract && (type.TypeKind == TypeKind.Class || type.IsRecord)) {
            for (var bt = type.BaseType; bt is not null; bt = bt.BaseType) {
                if (bt.OriginalDefinition.ToDisplayString() == StateFqn && bt.TypeArguments.Length == 1) {
                    var stateFqn = MakeGlobal(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                    if (!excludeFqns.Contains(stateFqn)) {
                        builder.Add(new(stateFqn, type.Name));
                    }

                    break;
                }
            }
        }

        foreach (var nested in type.GetTypeMembers()) {
            ProcessTypeForState(nested, builder, excludeFqns);
        }
    }

    static void ProcessNamespace(INamespaceSymbol ns, ImmutableArray<AggregateCandidate>.Builder builder) {
        foreach (var member in ns.GetMembers()) {
            switch (member) {
                case INamespaceSymbol child:
                    ProcessNamespace(child, builder);

                    break;
                case INamedTypeSymbol type:
                    ProcessType(type, builder);

                    break;
            }
        }
    }

    static void ProcessType(INamedTypeSymbol type, ImmutableArray<AggregateCandidate>.Builder builder) {
        if (type is { IsAbstract: false, TypeKind: TypeKind.Class }) {
            INamedTypeSymbol? stateType = null;

            for (var bt = type.BaseType; bt is not null; bt = bt.BaseType) {
                if (bt.OriginalDefinition.ToDisplayString() == AggregateFqn && bt.TypeArguments.Length == 1) {
                    stateType = bt.TypeArguments[0] as INamedTypeSymbol;

                    break;
                }
            }

            if (stateType is not null) {
                var methods = type.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(static m => m.MethodKind == MethodKind.Ordinary
                     && m is { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsImplicitlyDeclared: false }
                    )
                    .Select(static m => m.Name)
                    .Distinct()
                    .ToImmutableArray();

                var aggregateFqn = MakeGlobal(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                var stateFqn     = MakeGlobal(stateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                builder.Add(new(aggregateFqn, type.Name, stateFqn, stateType.Name, methods));
            }
        }

        foreach (var nested in type.GetTypeMembers()) {
            ProcessType(nested, builder);
        }
    }

    static void Generate(
            SourceProductionContext            spc,
            string                             assemblyName,
            ImmutableArray<AggregateCandidate> aggregates,
            ImmutableArray<StateCandidate>     states
        ) {
        if (aggregates.IsDefaultOrEmpty && states.IsDefaultOrEmpty) {
            const string marker = "// <auto-generated> SpyglassGenerator found no aggregates or states. </auto-generated>\n";
            spc.AddSource("Spyglass.Info.g.cs", marker);

            return;
        }

        var safeAssembly = SanitizeIdentifier(assemblyName);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Eventuous;");
        sb.AppendLine("using Eventuous.Spyglass;");
        sb.AppendLine();
        sb.AppendLine("namespace Eventuous.Spyglass.Generated;");
        sb.AppendLine();
        sb.AppendLine($"internal static class SpyglassModule_{safeAssembly} {{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize() {");

        if (!aggregates.IsDefaultOrEmpty) {
            foreach (var c in aggregates.Distinct()) {
                sb.AppendLine("        SpyglassRegistry.Register(new SpyglassAggregateInfo(");
                sb.AppendLine($"            \"{Escape(c.AggregateSimpleName)}\",");
                sb.AppendLine($"            \"{Escape(c.StateSimpleName)}\",");
                EmitStringArray(sb, c.Methods);
                sb.AppendLine($"            new {c.StateFqn}().GetRegisteredEventTypes().Select(t => t.Name).ToArray(),");
                sb.AppendLine($"            static (_, entityId) => StreamName.For<{c.AggregateFqn}>(entityId),");
                sb.AppendLine("            static async (eventStore, streamName, version) => {");
                sb.AppendLine("                var events = await eventStore.ReadStream(streamName, StreamReadPosition.Start, false, default);");
                sb.AppendLine("                if (events.Length == 0) return null;");
                sb.AppendLine($"                var aggregate = new {c.AggregateFqn}();");
                sb.AppendLine("                var selected = version == -1 ? events : events.Take(version + 1).ToArray();");
                sb.AppendLine("                aggregate.Load(selected.Length > 0 ? selected[^1].Revision : -1, selected.Select(x => x.Payload));");
                sb.AppendLine("                return new SpyglassLoadResult(");
                sb.AppendLine("                    aggregate.State,");
                sb.AppendLine("                    events.Select(x => new SpyglassEventInfo(x.Payload!.GetType().Name, x.Payload)).ToArray());");
                sb.AppendLine("            }");
                sb.AppendLine("        ));");
            }
        }

        if (!states.IsDefaultOrEmpty) {
            foreach (var s in states.Distinct()) {
                sb.AppendLine("        SpyglassRegistry.Register(new SpyglassAggregateInfo(");
                sb.AppendLine("            null,");
                sb.AppendLine($"            \"{Escape(s.StateSimpleName)}\",");
                sb.AppendLine("            System.Array.Empty<string>(),");
                sb.AppendLine($"            new {s.StateFqn}().GetRegisteredEventTypes().Select(t => t.Name).ToArray(),");
                sb.AppendLine($"            static (_, entityId) => StreamName.ForState<{s.StateFqn}>(entityId),");
                sb.AppendLine("            static async (eventStore, streamName, version) => {");
                sb.AppendLine("                var events = await eventStore.ReadStream(streamName, StreamReadPosition.Start, false, default);");
                sb.AppendLine("                if (events.Length == 0) return null;");
                sb.AppendLine("                var selected = version == -1 ? events : events.Take(version + 1).ToArray();");
                sb.AppendLine($"                var state = selected.Select(x => x.Payload!).Aggregate(new {s.StateFqn}(), (s, e) => s.When(e));");
                sb.AppendLine("                return new SpyglassLoadResult(");
                sb.AppendLine("                    state,");
                sb.AppendLine("                    events.Select(x => new SpyglassEventInfo(x.Payload!.GetType().Name, x.Payload)).ToArray());");
                sb.AppendLine("            }");
                sb.AppendLine("        ));");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource($"SpyglassModule_{safeAssembly}.g.cs", sb.ToString());
    }

    static void EmitStringArray(StringBuilder sb, ImmutableArray<string> items) {
        if (items.IsDefaultOrEmpty) {
            sb.AppendLine("            System.Array.Empty<string>(),");
        }
        else {
            sb.Append("            new string[] { ");
            sb.Append(string.Join(", ", items.Select(i => $"\"{Escape(i)}\"")));
            sb.AppendLine(" },");
        }
    }

    static string Escape(string s) => s.Replace("\\", @"\\").Replace("\"", "\\\"");

    static string MakeGlobal(string typeName) => !typeName.StartsWith("global::") ? $"global::{typeName}" : typeName;

    static string SanitizeIdentifier(string name) {
        var sb = new StringBuilder(name.Length);

        foreach (var ch in name) {
            if (ch is '.' or '-' or '+') sb.Append('_');
            else if (SyntaxFacts.IsIdentifierPartCharacter(ch)) sb.Append(ch);
            else sb.Append('_');
        }

        if (sb.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(sb[0])) sb.Insert(0, '_');

        return sb.ToString();
    }

    readonly record struct StateCandidate(string StateFqn, string StateSimpleName);

    readonly struct AggregateCandidate(string aggregateFqn, string aggregateSimpleName, string stateFqn, string stateSimpleName, ImmutableArray<string> methods)
        : IEquatable<AggregateCandidate> {
        public readonly string                 AggregateFqn        = aggregateFqn;
        public readonly string                 AggregateSimpleName = aggregateSimpleName;
        public readonly string                 StateFqn            = stateFqn;
        public readonly string                 StateSimpleName     = stateSimpleName;
        public readonly ImmutableArray<string> Methods             = methods;

        public bool Equals(AggregateCandidate other) => AggregateFqn == other.AggregateFqn;

        public override bool Equals(object? obj) => obj is AggregateCandidate other && Equals(other);

        public override int GetHashCode() => AggregateFqn?.GetHashCode() ?? 0;
    }
}
