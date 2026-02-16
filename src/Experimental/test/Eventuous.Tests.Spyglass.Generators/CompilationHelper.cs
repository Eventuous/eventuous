using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Eventuous.Spyglass.Generators;

namespace Eventuous.Tests.Spyglass.Generators;

static class CompilationHelper {
    public static CSharpCompilation CreateCompilation(string source) {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new(LanguageVersion.Preview));

        var refs = new List<MetadataReference> {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(State<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Aggregate<>).Assembly.Location)
        };

        TryAddRef(refs, "System.Runtime");
        TryAddRef(refs, "System.Collections");
        TryAddRef(refs, "System.Linq");
        TryAddRef(refs, "System.Private.CoreLib");

        return CSharpCompilation.Create(
            assemblyName: "SpyglassGeneratorTestAssembly",
            syntaxTrees: [syntaxTree],
            references: refs,
            options: new(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                specificDiagnosticOptions: [new("CS1701", ReportDiagnostic.Suppress)]
            )
        );
    }

    public static (string? GeneratedSource, Diagnostic[] Diagnostics) RunGenerator(CSharpCompilation compilation) {
        var generator    = new SpyglassGenerator().AsSourceGenerator();
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var driver       = CSharpGeneratorDriver.Create([generator], parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTree = outputCompilation.SyntaxTrees.FirstOrDefault(t => t.FilePath.Contains("SpyglassModule_"));

        return (generatedTree?.GetText().ToString(), diagnostics.ToArray());
    }

    static void TryAddRef(List<MetadataReference> refs, string asmName) {
        try {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == asmName);

            if (asm != null) refs.Add(MetadataReference.CreateFromFile(asm.Location));
        } catch {
            // ignore
        }
    }
}
