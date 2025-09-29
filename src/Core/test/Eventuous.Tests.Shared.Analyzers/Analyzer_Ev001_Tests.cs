// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using System.Runtime.CompilerServices;
using Eventuous.Shared.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Eventuous.Tests.Shared.Analyzers;

public class Analyzer_Ev001_Tests {
    [Test]
    public async Task Should_warn_for_unannotated_events_in_state_and_aggregate() {
        // Load the actual source from Analyzed.cs so the test reflects real code
        var source = LoadAnalyzedSource();

        var compilation = CreateCompilation(source);
        var analyzer    = new EventUsageAnalyzer();

        var diagnostics = await GetAnalyzerDiagnosticsAsync(compilation, analyzer);

        // We expect at least two EV001 diagnostics:
        // - State<TestState>.On<Events.RoomBooked>(...)
        // - Aggregate.Apply(new Events.RoomBooked(...))
        var ev001 = diagnostics.Where(d => d.Id == EventUsageAnalyzer.DiagnosticId).ToArray();

        await Assert.That(ev001.Length).IsGreaterThanOrEqualTo(2);

        // Optional: verify diagnostic messages mention the event type name
        await Assert.That(ev001.Any(d => d.GetMessage().Contains("RoomBooked"))).IsTrue();
    }

    static async Task<Diagnostic[]> GetAnalyzerDiagnosticsAsync(Compilation compilation, EventUsageAnalyzer analyzer) {
        var withAnalyzers = compilation.WithAnalyzers([analyzer]);
        var diagnostics   = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        // Filter out anything not from our analyzer id just in case
        return diagnostics.Where(d => d.Id == EventUsageAnalyzer.DiagnosticId).ToArray();
    }

    static string LoadAnalyzedSource([CallerFilePath] string? caller = null) {
        var dir = Path.GetDirectoryName(caller!)!;
        var path = Path.Combine(dir, "Analyzed.cs");
        return File.ReadAllText(path);
    }

    static CSharpCompilation CreateCompilation(string source) {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new(LanguageVersion.Preview));

        var refs = new List<MetadataReference> {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(State<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Aggregate<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(EventTypeAttribute).Assembly.Location),
        };

        // Some frameworks need additional facades depending on runtime; try to add them if present
        TryAddRef(refs, "System.Runtime");
        TryAddRef(refs, "System.Collections");
        TryAddRef(refs, "System.Linq");
        TryAddRef(refs, "System.Private.CoreLib");

        var compilation = CSharpCompilation.Create(
            assemblyName: "Analyzer_Ev001_Tests_Assembly",
            syntaxTrees: [syntaxTree],
            references: refs,
            options: new(OutputKind.DynamicallyLinkedLibrary,
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
        }
        catch {
            // ignore
        }
    }
}
