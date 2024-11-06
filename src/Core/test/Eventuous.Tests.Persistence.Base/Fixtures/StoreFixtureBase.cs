using System.Text.RegularExpressions;
using AutoFixture;
using Bogus;
using DotNet.Testcontainers.Containers;
using Eventuous.TestHelpers;
using Eventuous.TestHelpers.TUnit.Logging;
using MicroElements.AutoFixture.NodaTime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Persistence.Base.Fixtures;

public abstract class StoreFixtureBase {
    public           IEventStore     EventStore { get; protected private set; } = null!;
    public           IFixture        Auto       { get; }                        = new Fixture().Customize(new NodaTimeCustomization());
    protected static Faker           Faker      { get; }                        = new();
    protected        ServiceProvider Provider   { get; set; }                   = null!;
    protected        bool            AutoStart  { get; init; }                  = true;
    public           TypeMapper      TypeMapper { get; }                        = new();
}

public abstract partial class StoreFixtureBase<TContainer> : StoreFixtureBase, IAsyncInitializer, IAsyncDisposable where TContainer : DockerContainer {
    public virtual async Task InitializeAsync() {
        Container = CreateContainer();
        await Container.StartAsync();

        var services = new ServiceCollection();

        services.AddLogging(cfg => cfg.AddTUnit().SetMinimumLevel(LogLevel.Debug));

        Serializer = new DefaultEventSerializer(TestPrimitives.DefaultOptions, TypeMapper);
        services.AddSingleton(Serializer);
        services.AddSingleton(TypeMapper);
        SetupServices(services);

        Provider   = services.BuildServiceProvider();
        EventStore = Provider.GetRequiredService<IEventStore>();
        GetDependencies(Provider);

        if (AutoStart) {
            await Start();
        }
    }

    protected async Task Start() {
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StartAsync(default);
        }
    }

    public virtual async ValueTask DisposeAsync() {
        if (_disposed) return;

        _disposed = true;
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StopAsync(default);
        }

        await Provider.DisposeAsync();
        await Container.DisposeAsync();
    }

    protected abstract void SetupServices(IServiceCollection services);

    protected abstract TContainer CreateContainer();

    protected virtual void GetDependencies(IServiceProvider provider) { }

    public TContainer Container { get; private set; } = null!;

    public IEventSerializer Serializer { get; private set; } = null!;

    bool _disposed;

    public static string GetSchemaName() => NormaliseRegex().Replace(new Faker().Internet.UserName(), "").ToLower();

#if NET8_0_OR_GREATER
    [GeneratedRegex(@"[\.\-\s]")]
    private static partial Regex NormaliseRegex();
#else
    static Regex NormaliseRegex() => new(@"[\.\-\s]");
#endif
}
