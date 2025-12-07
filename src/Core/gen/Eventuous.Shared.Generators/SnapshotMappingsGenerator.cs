// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;
using static Eventuous.Shared.Generators.Constants;
using static Eventuous.Shared.Generators.Helpers;

namespace Eventuous.Shared.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class SnapshotMappingsGenerator : IIncrementalGenerator {

    static Map? GetMapFromSnapshotsAttribute(GeneratorSyntaxContext context) {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classSyntax);
        if (classSymbol == null || !IsState(classSymbol)) return null;

        var snapshotsAttr = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "SnapshotsAttribute");

        if (snapshotsAttr == null) return null;
        
        var snapshotTypes = new HashSet<string>();

        foreach (var arg in snapshotsAttr.ConstructorArguments) {
            if (arg.Kind == TypedConstantKind.Array) {
                foreach (var typeConstant in arg.Values) {
                    if (typeConstant.Value is ITypeSymbol typeSymbol) {
                        snapshotTypes.Add(MakeGlobal(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    }
                }
            }
            else if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol singleType) {
                snapshotTypes.Add(MakeGlobal(singleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
        }

        var storageStrategy = GetStorageStrategyFromAttribute(snapshotsAttr);

        var stateType = classSymbol.BaseType?.TypeArguments[0];
        if (stateType == null) return null;

        return new Map {
            SnapshotTypes = snapshotTypes,
            StateType = MakeGlobal(classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
            StorageStrategy = storageStrategy
        };
    }

    static HashSet<string> GetTypesFromSnapshotsAttribute(AttributeData attributeData) {
        var result = new HashSet<string>();

        foreach (var arg in attributeData.ConstructorArguments) {
            if (arg.Kind == TypedConstantKind.Array) {
                foreach (var typeConstant in arg.Values) {
                    if (typeConstant.Value is ITypeSymbol typeSymbol) {
                        result.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    }
                }
            }
            else if (arg.Kind == TypedConstantKind.Type) {
                if (arg.Value is ITypeSymbol typeSymbol) {
                    result.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }
        }

        foreach (var namedArg in attributeData.NamedArguments) {
            if (namedArg.Value.Kind == TypedConstantKind.Array) {
                foreach (var typeConstant in namedArg.Value.Values) {
                    if (typeConstant.Value is ITypeSymbol typeSymbol) {
                        result.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    }
                }
            }
            else if (namedArg.Value.Kind == TypedConstantKind.Type && namedArg.Value.Value is ITypeSymbol typeSymbol) {
                result.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
        }

        return result;
    }

    static ImmutableArray<Map> GetMapFromSnapshotsAttribute(Compilation compilation) {
        var builder = ImmutableArray.CreateBuilder<Map>();

        // Current assembly
        ProcessNamespace(compilation.Assembly.GlobalNamespace);

        // Referenced assemblies
        foreach (var ra in compilation.SourceModule.ReferencedAssemblySymbols) {
            ProcessNamespace(ra.GlobalNamespace);
        }

        return builder.ToImmutable();

        void ProcessType(INamedTypeSymbol type) {
            var attr = GetSnapshotsAttribute(type);
            if (attr is not null) {
                var map = new Map {
                    SnapshotTypes = GetTypesFromSnapshotsAttribute(attr),
                    StateType = MakeGlobal(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                    StorageStrategy = GetStorageStrategyFromAttribute(attr)
                };

                builder.Add(map);
            }

            foreach (var nt in type.GetTypeMembers()) {
                ProcessType(nt);
            }
        }

        void ProcessNamespace(INamespaceSymbol ns) {
            foreach (var member in ns.GetMembers()) {
                switch (member) {
                    case INamespaceSymbol cns:
                        ProcessNamespace(cns); break;
                    case INamedTypeSymbol type:
                        ProcessType(type); break;
                }
            }
        }
    }

    static void Output(SourceProductionContext context, ImmutableArray<Map> maps) {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#pragma warning disable CS8019");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace Eventuous;");
        sb.AppendLine();
        sb.AppendLine("internal static class SnapshotTypeMappings {");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize() {");

        foreach (var map in maps) {
            var strategyValue = map.StorageStrategy switch {
                "SameStream" => "SnapshotStorageStrategy.SameStream",
                "SeparateStream" => "SnapshotStorageStrategy.SeparateStream",
                "SeparateStore" => "SnapshotStorageStrategy.SeparateStore",
                _ => "SnapshotStorageStrategy.SameStream"
            };

            foreach (var snapshotType in map.SnapshotTypes) {
                sb.AppendLine($"        SnapshotTypeMap.Register(typeof({map.StateType}), typeof({snapshotType}), {strategyValue});");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SnapshotTypeMappings.g.cs", sb.ToString());
    }

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var maps = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (c, _) => GetMapFromSnapshotsAttribute(c))
            .Where(static m => m is not null)
            .Collect();

        var mapsFromReferencedAssmeblies = context.CompilationProvider
            .Select(static (c, _) => GetMapFromSnapshotsAttribute(c));

        var mergedMaps = maps
            .Combine(mapsFromReferencedAssmeblies)
            .Select(static (pair, _) => pair.Left.AddRange((IEnumerable<Map>)pair.Right));

        context.RegisterSourceOutput(mergedMaps, Output!);

    }

    static bool IsState(INamedTypeSymbol type) {
        var baseType = type.BaseType;

        return baseType is not null
            && baseType.Name == StateType
            && baseType.ContainingNamespace.ToDisplayString() == BaseNamespace
            && baseType.IsGenericType;
    }

    static AttributeData? GetSnapshotsAttribute(ISymbol symbol) {
        foreach (var data in symbol.GetAttributes()) {
            var attrClass = data.AttributeClass;
            if (attrClass is null) continue;

            var name = attrClass.ToDisplayString();
            if (name == SnapshotsAttrFqcn || attrClass.Name is SnapshotsAttribute) return data;
        }

        return null;
    }

    static string GetStorageStrategyFromAttribute(AttributeData attributeData) {
        foreach (var namedArg in attributeData.NamedArguments) {
            if (namedArg.Key == "StorageStrategy" && namedArg.Value.Kind == TypedConstantKind.Enum) {
                // Try to get the enum value name from the typed constant
                var enumType = namedArg.Value.Type;
                if (enumType != null) {
                    // Get the enum value as a string representation
                    var enumValue = namedArg.Value.Value;
                    if (enumValue != null) {
                        // Try to find the enum member with this value
                        var enumMembers = enumType.GetMembers().OfType<IFieldSymbol>()
                            .Where(f => f.IsStatic && f.IsDefinition && f.ConstantValue != null);
                        
                        foreach (var member in enumMembers) {
                            if (Equals(member.ConstantValue, enumValue)) {
                                return member.Name;
                            }
                        }
                        
                        // Fallback: map numeric values
                        if (enumValue is int intValue) {
                            return intValue switch {
                                0 => "SameStream",
                                1 => "SeparateStream",
                                2 => "SeparateStore",
                                _ => "SameStream"
                            };
                        }
                    }
                }
            }
        }

        return "SameStream"; // Default value
    }

    sealed record Map {
        public string StateType { get; set; } = null!;
        public HashSet<string> SnapshotTypes { get; set; } = [];
        public string StorageStrategy { get; set; } = "SameStream";
    }
}