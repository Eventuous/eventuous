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
    public           IEventStore     EventStore { get; protected private set; } = null!;
    protected static Faker           Faker      { get; }                        = new();
    protected        ServiceProvider Provider   { get; set; }                   = null!;
    protected        bool            AutoStart  { get; init; }                  = true;
    public           TypeMapper      TypeMapper { get; }                        = new();
}

public abstract partial class StoreFixtureBase<TContainer>(LogLevel logLevel) : StoreFixtureBase, IStartableFixture where TContainer : DockerContainer {
    public virtual async Task InitializeAsync() {
        Container = CreateContainer();
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
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StopAsync(CancellationToken.None);
        }

        await Provider.DisposeAsync();
        await Container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected abstract void SetupServices(IServiceCollection services);

    protected abstract TContainer CreateContainer();

    protected virtual void GetDependencies(IServiceProvider provider) { }

    public TContainer Container { get; private set; } = null!;

    public IEventSerializer Serializer { get; private set; } = null!;

    bool _disposed;

    protected static string GetSchemaName() => NormaliseRegex().Replace(new Faker().Internet.UserName(), "").ToLower();

#if NET8_0_OR_GREATER
    [GeneratedRegex(@"[\.\-\s]")]
    private static partial Regex NormaliseRegex();
#else
    static Regex NormaliseRegex() => new(@"[\.\-\s]");
#endif
}
