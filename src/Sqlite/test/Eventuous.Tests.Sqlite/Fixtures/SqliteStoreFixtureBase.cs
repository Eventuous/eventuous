using System.Text.RegularExpressions;
using Bogus;
using Eventuous.TestHelpers;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Tests.Sqlite.Fixtures;

public abstract partial class SqliteStoreFixtureBase(LogLevel logLevel = LogLevel.Information)
    : StoreFixtureBase, IStartableFixture {
    string _dbPath = null!;

    public string ConnectionString { get; private set; } = null!;

    public virtual async Task InitializeAsync() {
        _dbPath = Path.Combine(Path.GetTempPath(), $"eventuous_test_{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={_dbPath}";

        var services = new ServiceCollection();

        Serializer = new DefaultEventSerializer(TestPrimitives.DefaultOptions, TypeMapper);
        services.AddSingleton(Serializer);
        services.AddSingleton(TypeMapper);
        services.AddLogging(b => b.ForTests(logLevel).SetMinimumLevel(logLevel));
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

    public virtual async ValueTask DisposeAsync() {
        if (_disposed) return;

        _disposed = true;
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StopAsync(CancellationToken.None);
        }

        await Provider.DisposeAsync();

        // Clean up temp database files
        TryDeleteFile(_dbPath);
        TryDeleteFile(_dbPath + "-wal");
        TryDeleteFile(_dbPath + "-shm");

        GC.SuppressFinalize(this);
    }

    static void TryDeleteFile(string path) {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    protected abstract void SetupServices(IServiceCollection services);

    protected virtual void GetDependencies(IServiceProvider provider) { }

    public IEventSerializer Serializer { get; private set; } = null!;

    bool _disposed;

    protected static string GetSchemaName() => NormaliseRegex().Replace(new Faker().Internet.UserName(), "").ToLower();

    [GeneratedRegex(@"[\.\-\s]")]
    private static partial Regex NormaliseRegex();
}
