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

    [Test]
    public async Task Should_keep_discovery_order_for_types_of_equal_specificity() {
        // Types related only through generic variance have the same number of supertypes, so their relative order
        // must stay as declared. Names are chosen so that alphabetical order would put the broader type first.
        const string source = """
                              using Eventuous.Subscriptions.Context;

                              namespace Foo;

                              public class Animal;

                              public class Zebra : Animal;

                              public interface IEnvelope<out T>;

                              public static class Usages {
                                  public static void Zebras(IMessageConsumeContext<IEnvelope<Zebra>> ctx) { }

                                  public static void Animals(IMessageConsumeContext<IEnvelope<Animal>> ctx) { }
                              }
                              """;

        var (generated, errors) = RunGenerator(source);

        await Assert.That(errors).IsEmpty();
        await Assert.That(ArmIndex(generated, "global::Foo.IEnvelope<global::Foo.Zebra>")).IsLessThan(ArmIndex(generated, "global::Foo.IEnvelope<global::Foo.Animal>"));
    }

    [Test]
    [Arguments("string", "global::System.String")]
    [Arguments("object", "global::System.Object")]
    [Arguments("string[]", "global::System.String[]")]
    public async Task Should_emit_compilable_arm_for_keyword_message_type(string messageType, string expectedArmType) {
        // Issue #593: keyword types have no namespace to qualify, and 'global::string' is not valid C#
        var source = $$"""
                       using Eventuous.Subscriptions.Context;

                       namespace Foo;

                       public static class Usages {
                           public static void Use(IMessageConsumeContext<{{messageType}}> ctx) { }
                       }
                       """;

        var (generated, errors) = RunGenerator(source);

        await Assert.That(errors).IsEmpty();
        await Assert.That(ArmIndex(generated, expectedArmType)).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Should_not_emit_arm_for_dynamic_message_type() {
        // 'dynamic' can be neither qualified nor used in a type pattern
        const string source = """
                              using Eventuous;
                              using Eventuous.Subscriptions.Context;

                              namespace Foo;

                              [EventType("V1.RoomBooked")]
                              public record RoomBooked;

                              public static class Usages {
                                  public static void Use(IMessageConsumeContext<dynamic> ctx) { }
                              }
                              """;

        var (generated, errors) = RunGenerator(source);

        await Assert.That(errors).IsEmpty();
        await Assert.That(generated).DoesNotContain("dynamic");
    }

    [Test]
    public async Task Should_emit_object_after_interfaces() {
        // Issue #594: an interface converts to object although object is not among its base types,
        // and object is discovered first here
        const string source = """
                              using Eventuous.Subscriptions.Context;

                              namespace Foo;

                              public interface IFoo;

                              public static class Usages {
                                  public static void Any(IMessageConsumeContext<object> ctx) { }

                                  public static void Foo(IMessageConsumeContext<IFoo> ctx) { }
                              }
                              """;

        var (generated, errors) = RunGenerator(source);

        await Assert.That(errors).IsEmpty();
        await Assert.That(ArmIndex(generated, "global::Foo.IFoo")).IsLessThan(ArmIndex(generated, "global::System.Object"));
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
