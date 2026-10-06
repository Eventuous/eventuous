using Eventuous.Diagnostics;
using Eventuous.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Eventuous.Tests.DependencyInjection;

// Diagnostics is process-wide. No other tests may observe the temporary setting.
[NotInParallel]
public class StoreRegistrationTests {
    [Test]
    [Arguments(false, false, Registration.Store)]
    [Arguments(false, true, Registration.Store)]
    [Arguments(true, false, Registration.Store)]
    [Arguments(true, true, Registration.Store)]
    [Arguments(false, false, Registration.ReaderWriter)]
    [Arguments(false, true, Registration.ReaderWriter)]
    [Arguments(true, false, Registration.ReaderWriter)]
    [Arguments(true, true, Registration.ReaderWriter)]
    [Arguments(false, false, Registration.Separate)]
    [Arguments(false, true, Registration.Separate)]
    [Arguments(true, false, Registration.Separate)]
    [Arguments(true, true, Registration.Separate)]
    public async Task WrittenEventsAreVisibleThroughEveryRegisteredInterface(bool tracing, bool factory, Registration registration) {
        using var diagnostics = new DiagnosticsSetting(tracing);
        var factoryCalls = 0;
        using var host = new HostBuilder().ConfigureServices(services => {
            Register(services, registration, factory, _ => { factoryCalls++; return new(); });
        }).Build();
        await host.StartAsync();

        var writer = host.Services.GetRequiredService<IEventWriter>();
        var reader = host.Services.GetRequiredService<IEventReader>();
        var stream = new StreamName("shared-store");
        await Append(writer, stream, "first");
        (await Read(reader, stream)).ShouldBe(new[] { "first" });
        if (registration == Registration.Store) {
            var store = host.Services.GetRequiredService<IEventStore>();
            (await store.StreamExists(stream)).ShouldBeTrue();
            await Append(store, stream, "second", new(0));
            (await Read(reader, stream)).ShouldBe(new[] { "first", "second" });
        }

        using (var scope = host.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<IEventReader>().ShouldBeSameAs(reader);
            scope.ServiceProvider.GetRequiredService<IEventWriter>().ShouldBeSameAs(writer);
        }
        if (factory) factoryCalls.ShouldBe(1);
        await host.StopAsync();
    }

    [Test]
    [Arguments(false, false, true)]
    [Arguments(false, true, true)]
    [Arguments(true, false, true)]
    [Arguments(true, true, true)]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    public async Task StandaloneRegistrationUsesTheExistingBackingStore(bool tracing, bool factory, bool readerOnly) {
        using var diagnostics = new DiagnosticsSetting(tracing);
        var backing = new InMemoryEventStore();
        var stream = new StreamName("standalone-store");
        var factoryCalls = 0;
        using var host = new HostBuilder().ConfigureServices(services => {
            services.AddSingleton(backing);
            InMemoryEventStore Create(IServiceProvider _) { factoryCalls++; return new(); }
            if (readerOnly) {
                if (factory) services.AddEventReader<InMemoryEventStore>(Create);
                else services.AddEventReader<InMemoryEventStore>();
            }
            else {
                if (factory) services.AddEventWriter<InMemoryEventStore>(Create);
                else services.AddEventWriter<InMemoryEventStore>();
            }
        }).Build();
        await host.StartAsync();

        if (readerOnly) {
            await Append(backing, stream, "seeded");
            (await Read(host.Services.GetRequiredService<IEventReader>(), stream)).ShouldBe(new[] { "seeded" });
        }
        else {
            await Append(host.Services.GetRequiredService<IEventWriter>(), stream, "written");
            (await Read(backing, stream)).ShouldBe(new[] { "written" });
        }
        factoryCalls.ShouldBe(0);
        await host.StopAsync();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task RepeatedRegistrationKeepsOneServiceAndOneFactory(bool tracing, bool factory) {
        using var diagnostics = new DiagnosticsSetting(tracing);
        var firstCalls = 0;
        var secondCalls = 0;
        using var host = new HostBuilder().ConfigureServices(services => {
            Register(services, Registration.Store, factory, _ => { firstCalls++; return new(); });
            Register(services, Registration.Store, factory, _ => { secondCalls++; return new(); });
        }).Build();
        await host.StartAsync();

        var writer = host.Services.GetServices<IEventWriter>().ShouldHaveSingleItem();
        var reader = host.Services.GetServices<IEventReader>().ShouldHaveSingleItem();
        var store = host.Services.GetServices<IEventStore>().ShouldHaveSingleItem();
        var stream = new StreamName("repeated-store");
        await Append(writer, stream, "once");
        (await Read(reader, stream)).ShouldBe(new[] { "once" });
        (await store.StreamExists(stream)).ShouldBeTrue();
        firstCalls.ShouldBe(factory ? 1 : 0);
        secondCalls.ShouldBe(0);
        await host.StopAsync();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task RegistrationPreservesExplicitInterfaceOverrides(bool tracing, bool factory) {
        using var diagnostics = new DiagnosticsSetting(tracing);
        var existing = new InMemoryEventStore();
        var factoryCalls = 0;
        using var host = new HostBuilder().ConfigureServices(services => {
            services.AddSingleton<IEventStore>(existing);
            services.AddSingleton<IEventReader>(existing);
            services.AddSingleton<IEventWriter>(existing);
            Register(services, Registration.Store, factory, _ => { factoryCalls++; return new(); });
        }).Build();
        await host.StartAsync();

        var stream = new StreamName("explicit-store");
        await Append(host.Services.GetRequiredService<IEventWriter>(), stream, "preserved");
        (await Read(existing, stream)).ShouldBe(new[] { "preserved" });
        host.Services.GetRequiredService<IEventReader>().ShouldBeSameAs(existing);
        host.Services.GetRequiredService<IEventStore>().ShouldBeSameAs(existing);
        factoryCalls.ShouldBe(0);
        await host.StopAsync();
    }

    static void Register(IServiceCollection services, Registration registration, bool factory, Func<IServiceProvider, InMemoryEventStore> create) {
        switch (registration) {
            case Registration.Store:
                if (factory) services.AddEventStore(create);
                else services.AddEventStore<InMemoryEventStore>();
                break;
            case Registration.ReaderWriter:
                if (factory) services.AddEventReaderWriter(create);
                else services.AddEventReaderWriter<InMemoryEventStore>();
                break;
            case Registration.Separate:
                if (factory) services.AddEventReader(create).AddEventWriter(create);
                else services.AddEventReader<InMemoryEventStore>().AddEventWriter<InMemoryEventStore>();
                break;
        }
    }

    static Task<AppendEventsResult> Append(IEventWriter writer, StreamName stream, string value, ExpectedStreamVersion? version = null)
        => writer.AppendEvents(stream, version ?? ExpectedStreamVersion.NoStream, new[] { new NewStreamEvent(Guid.NewGuid(), value, new()) }, default);

    static async Task<string[]> Read(IEventReader reader, StreamName stream)
        => (await reader.ReadEvents(stream, StreamReadPosition.Start, 10, true, default)).Select(x => (string)x.Payload!).ToArray();

    public enum Registration { Store, ReaderWriter, Separate }

    sealed class DiagnosticsSetting : IDisposable {
        readonly bool _previous = EventuousDiagnostics.Enabled;
        public DiagnosticsSetting(bool enabled) => Set(enabled);
        public void Dispose() => Set(_previous);
        static void Set(bool enabled) {
            if (enabled) EventuousDiagnostics.Enable();
            else EventuousDiagnostics.Disable();
        }
    }
}
