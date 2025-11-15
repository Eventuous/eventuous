using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Diags = Eventuous.Extensions.AspNetCore.Generators.Diagnostics;

namespace Eventuous.Tests.Extensions.AspNetCore.Analyzers;

public partial class HttpCommandAnnotationTests {
    static async Task<Diagnostic[]> GetAnalyzerDiagnosticsAsync(Compilation compilation, DiagnosticAnalyzer analyzer) {
        var withAnalyzers = compilation.WithAnalyzers([analyzer]);
        var diagnostics   = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);

        return diagnostics.Where(d => d.Id is Diags.DiagnosticId or Diags.RouteDiagnosticId).ToArray();
    }

    static CSharpCompilation CreateCompilation(string source) {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new(LanguageVersion.Preview));

        var refs = new List<MetadataReference> {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(State<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Eventuous.Extensions.AspNetCore.Http.CommandServiceRouteBuilder<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder).Assembly.Location),
            // Additional ASP.NET Core references used in method signatures to enable full symbol binding
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Builder.RouteHandlerBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly.Location)
        };

        TryAddRef(refs, "System.Runtime");
        TryAddRef(refs, "System.Collections");
        TryAddRef(refs, "System.Linq");
        TryAddRef(refs, "System.Private.CoreLib");

        var compilation = CSharpCompilation.Create(
            assemblyName: "Analyzer_Evta_Tests_Assembly",
            syntaxTrees: [syntaxTree],
            references: refs,
            options: new(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                specificDiagnosticOptions: [new("CS1701", ReportDiagnostic.Suppress)]
            )
        );

        return compilation;
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
