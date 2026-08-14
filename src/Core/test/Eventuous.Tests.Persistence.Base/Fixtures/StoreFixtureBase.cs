using System.Text.RegularExpressions;
using Bogus;
using DotNet.Testcontainers.Containers;
using Eventuous.TestHelpers;
using Eventuous.TestHelpers.TUnit.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Persistence.Base.Fixtures;

public interface IStartableFixture : IAsyncInitializer, IAsyncDisposable;

public abstract class StoreFixtureBase {
    public           IEventStore     EventStore { get; protected set; } = null!;
    protected static Faker           Faker      { get; }                        = new();
    protected        ServiceProvider Provider   { get; set; }                   = null!;
    protected        bool            AutoStart  { get; init; }                  = true;
    public           TypeMapper      TypeMapper { get; }                        = new();
}

public abstract partial class StoreFixtureBase<TContainer>(LogLevel logLevel) : StoreFixtureBase, IStartableFixture where TContainer : DockerContainer {
    public virtual async Task InitializeAsync() {
        // Initialising twice is a restart, and tests do it — the previous round's container and provider
        // must be released before these properties are overwritten, or they're abandoned.
        if (_initialized) await Teardown();

        _initialized = true;
        Container    = CreateContainer();
        await Container.StartAsync();

        var services = new ServiceCollection();

        Serializer = new DefaultEventSerializer(TestPrimitives.DefaultOptions, TypeMapper);
        services.AddSingleton(Serializer);
        services.AddSingleton(TypeMapper);
        services.AddLogging(b => ConfigureLogging(b.ForTests(logLevel)).SetMinimumLevel(logLevel));
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
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    protected virtual ILoggingBuilder ConfigureLogging(ILoggingBuilder builder) => builder;

    public virtual async ValueTask DisposeAsync() {
        if (_disposed) return;

        _disposed = true;
        await Teardown();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases one round's container and provider. Tolerant of a half-built fixture — a failed
    /// <see cref="InitializeAsync"/> may need to release a container without ever having built a provider.
    /// </summary>
    async ValueTask Teardown() {
        var provider  = Provider;
        var container = Container;

        // Cleared before releasing, so a partial failure here doesn't find round one's disposed state again.
        Provider  = null!;
        Container = null!;

        try {
            if (provider is not null) {
                foreach (var hostedService in provider.GetServices<IHostedService>()) {
                    await hostedService.StopAsync(CancellationToken.None);
                }

                await provider.DisposeAsync();
            }
        } finally {
            if (container is not null) await container.DisposeAsync();
        }
    }

    protected abstract void SetupServices(IServiceCollection services);

    protected abstract TContainer CreateContainer();

    protected virtual void GetDependencies(IServiceProvider provider) { }

    public TContainer Container { get; private set; } = null!;

    public IEventSerializer Serializer { get; private set; } = null!;

    bool _disposed;
    bool _initialized;

    protected static string GetSchemaName() => NormaliseRegex().Replace(new Faker().Internet.UserName(), "").ToLower();

    [GeneratedRegex(@"[\.\-\s]")]
    private static partial Regex NormaliseRegex();
}
