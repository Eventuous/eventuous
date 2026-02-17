namespace Eventuous.Tests.Spyglass.Generators;

public class SpyglassGeneratorTests {
    [Test]
    public async Task Should_discover_aggregate_with_state() {
        const string source = """
            using Eventuous;

            public record TestState : State<TestState> {
                public TestState() {
                    On<TestEvent>((state, _) => state);
                }
            }

            public class TestEvent {}

            public class TestAggregate : Aggregate<TestState> {
                public void DoSomething() {}
            }
            """;

        var compilation = CompilationHelper.CreateCompilation(source);
        var (generated, diagnostics) = CompilationHelper.RunGenerator(compilation);

        await Assert.That(diagnostics).IsEmpty();
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated!).Contains("\"TestAggregate\"");
        await Assert.That(generated).Contains("\"TestState\"");
        await Assert.That(generated).Contains("StreamName.For<global::TestAggregate>(entityId)");
        await Assert.That(generated).Contains("new global::TestAggregate()");
        await Assert.That(generated).Contains("aggregate.Load(");
    }

    [Test]
    public async Task Should_discover_standalone_state() {
        const string source = """
            using Eventuous;

            public record PaymentState : State<PaymentState> {
                public PaymentState() {
                    On<PaymentRecorded>((state, _) => state);
                }
            }

            public class PaymentRecorded {}
            """;

        var compilation = CompilationHelper.CreateCompilation(source);
        var (generated, diagnostics) = CompilationHelper.RunGenerator(compilation);

        await Assert.That(diagnostics).IsEmpty();
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated!).Contains("null,");
        await Assert.That(generated).Contains("\"PaymentState\"");
        await Assert.That(generated).Contains("StreamName.ForState<global::PaymentState>(entityId)");
        await Assert.That(generated).Contains("s.When(e)");
        await Assert.That(generated).DoesNotContain("aggregate.Load(");
    }

    [Test]
    public async Task Should_collect_public_methods_from_aggregate() {
        const string source = """
            using Eventuous;

            public record SomeState : State<SomeState> {}

            public class SomeAggregate : Aggregate<SomeState> {
                public void BookRoom() {}
                public void CancelBooking() {}
            }
            """;

        var compilation = CompilationHelper.CreateCompilation(source);
        var (generated, _) = CompilationHelper.RunGenerator(compilation);

        await Assert.That(generated).IsNotNull();
        await Assert.That(generated!).Contains("\"BookRoom\"");
        await Assert.That(generated).Contains("\"CancelBooking\"");
    }

    [Test]
    public async Task Should_emit_marker_when_no_types_found() {
        const string source = """
            public class Nothing {}
            """;

        var compilation = CompilationHelper.CreateCompilation(source);
        var (generated, diagnostics) = CompilationHelper.RunGenerator(compilation);

        await Assert.That(diagnostics).IsEmpty();
        // No SpyglassModule_ file should be generated; only the marker comment
        await Assert.That(generated).IsNull();
    }

    [Test]
    public async Task Should_exclude_abstract_types() {
        const string source = """
            using Eventuous;

            public abstract record BaseState : State<BaseState> {}

            public abstract class BaseAggregate : Aggregate<BaseState> {}
            """;

        var compilation = CompilationHelper.CreateCompilation(source);
        var (generated, diagnostics) = CompilationHelper.RunGenerator(compilation);

        await Assert.That(diagnostics).IsEmpty();
        await Assert.That(generated).IsNull();
    }

    [Test]
    public async Task Should_not_register_state_as_standalone_when_aggregate_exists() {
        const string source = """
            using Eventuous;

            public record OrderState : State<OrderState> {
                public OrderState() {
                    On<OrderPlaced>((state, _) => state);
                }
            }

            public class OrderPlaced {}

            public class Order : Aggregate<OrderState> {
                public void PlaceOrder() {}
            }
            """;

        var compilation = CompilationHelper.CreateCompilation(source);
        var (generated, _) = CompilationHelper.RunGenerator(compilation);

        await Assert.That(generated).IsNotNull();
        // Should register as aggregate (not standalone)
        await Assert.That(generated!).Contains("\"Order\"");
        // The null standalone registration should not appear
        var registrations = generated.Split("SpyglassRegistry.Register(");
        // One split before the first Register + one registration = 2 parts
        await Assert.That(registrations.Length).IsEqualTo(2);
    }
}
