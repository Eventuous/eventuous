using Eventuous.Subscriptions.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Eventuous.Tests.Subscriptions;

public class ConsumeContextConverterGeneratorTests {
    [Test]
    public async Task Should_emit_derived_event_type_before_its_base() {
        // Issue #589: the base type lives in an outer namespace, so discovery finds it first
        const string source = """
                              using Eventuous;

                              namespace Foo {
                                  [EventType("V1.Base")]
                                  public record BaseEvent;
                              }

                              namespace Foo.Bar {
                                  [EventType("V1.Derived")]
                                  public sealed record DerivedEvent : Foo.BaseEvent;
                              }
                              """;

        var (generated, errors) = RunGenerator(source);

        await Assert.That(errors).IsEmpty();
        await Assert.That(ArmIndex(generated, "global::Foo.Bar.DerivedEvent")).IsLessThan(ArmIndex(generated, "global::Foo.BaseEvent"));
    }

    [Test]
    public async Task Should_emit_interfaces_after_their_implementations_and_derived_interfaces() {
        // Names are chosen so that alphabetical order is the opposite of the required one
        const string source = """
                              using Eventuous;
                              using Eventuous.Subscriptions.Context;

                              namespace Foo;

                              public interface IAnyEvent;

                              public interface IBookingEvent : IAnyEvent;

                              [EventType("V1.RoomBooked")]
                              public record RoomBooked : IBookingEvent;

                              public static class Usages {
                                  public static void Any(IMessageConsumeContext<IAnyEvent> ctx) { }

                                  public static void Booking(IMessageConsumeContext<IBookingEvent> ctx) { }
                              }
                              """;

        var (generated, errors) = RunGenerator(source);

        await Assert.That(errors).IsEmpty();
        await Assert.That(ArmIndex(generated, "global::Foo.RoomBooked")).IsLessThan(ArmIndex(generated, "global::Foo.IBookingEvent"));
        await Assert.That(ArmIndex(generated, "global::Foo.IBookingEvent")).IsLessThan(ArmIndex(generated, "global::Foo.IAnyEvent"));
    }

    static int ArmIndex(string generated, string typeName) {
        var index = generated.IndexOf($"{typeName} =>", StringComparison.Ordinal);

        return index >= 0 ? index : throw new InvalidOperationException($"No switch arm generated for {typeName}");
    }

    static (string Generated, Diagnostic[] Errors) RunGenerator(string source) {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var runtimeDir   = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var refs = Directory.GetFiles(runtimeDir, "System.*.dll")
            .Append(typeof(EventTypeAttribute).Assembly.Location)
            .Append(typeof(Eventuous.Subscriptions.EventHandler).Assembly.Location)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            assemblyName: "ConsumeContextConverterGeneratorTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: refs,
            options: new(OutputKind.DynamicallyLinkedLibrary, specificDiagnosticOptions: [new("CS1701", ReportDiagnostic.Suppress)])
        );

        var driver = CSharpGeneratorDriver.Create([new ConsumeContextConverterGenerator().AsSourceGenerator()], parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = output.SyntaxTrees.Single(t => t.FilePath.EndsWith("MessageConsumeContext_Converters.g.cs"));
        var errors    = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        return (generated.GetText().ToString(), errors);
    }
}
