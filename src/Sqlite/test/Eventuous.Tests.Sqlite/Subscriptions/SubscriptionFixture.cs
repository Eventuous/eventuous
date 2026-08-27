using System.Text.RegularExpressions;
using Bogus;
using Eventuous.Sqlite;
using Eventuous.Sqlite.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Eventuous.Tests.Sqlite.Subscriptions;

public partial class SubscriptionFixture<TSubscription, TSubscriptionOptions, TEventHandler>(
        Action<TSubscriptionOptions> configureOptions,
        bool                         autoStart         = true,
        Action<IServiceCollection>?  configureServices = null,
        LogLevel                     logLevel          = LogLevel.Information
    )
    : IStartableFixture
    where TSubscription : SqliteSubscriptionBase<TSubscriptionOptions>
    where TSubscriptionOptions : SqliteSubscriptionBaseOptions
    where TEventHandler : class, IEventHandler {
    string _dbPath = null!;

    readonly string _schemaName = GetSchemaName();

    string                    ConnectionString { get; set; }         = null!;
    TypeMapper                TypeMapper       { get; }              = new();
    public   IEventStore      EventStore       { get; private set; } = null!;
    public   string           SubscriptionId   { get; }              = $"test-{Guid.NewGuid():N}";
    internal TEventHandler    Handler          { get; private set; } = null!;
    internal ICheckpointStore CheckpointStore  { get; private set; } = null!;
    ILoggerFactory            LoggerFactory    { get; set; }         = null!;
    ILogger                   Log              { get; set; }         = null!;
    IMessageSubscription      Subscription     { get; set; }         = null!;
    ServiceProvider           Provider         { get; set; }         = null!;

    public async Task InitializeAsync() {
        _dbPath          = Path.Combine(Path.GetTempPath(), $"eventuous_test_{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={_dbPath}";

        TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);

        var services   = new ServiceCollection();
        var serializer = new DefaultEventSerializer(TestPrimitives.DefaultOptions, TypeMapper);
        services.AddSingleton<IEventSerializer>(serializer);
        services.AddSingleton(TypeMapper);
        services.AddLogging(b => b.ForTests(logLevel).SetMinimumLevel(logLevel));

        services.AddEventuousSqlite(ConnectionString, _schemaName, true);
        services.AddEventStore<SqliteStore>();
        services.AddSqliteCheckpointStore();
        services.AddSingleton(new TestEventHandlerOptions());

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            SubscriptionId,
            b => {
                b.AddEventHandler<TEventHandler>();

                b.Configure(opt => {
                        opt.Schema           = _schemaName;
                        opt.ConnectionString = ConnectionString;
                        configureOptions(opt);
                    }
                );
            }
        );

        // Remove the SubscriptionHostedService registration since we manage the subscription lifecycle manually
        var host = services.First(x => !x.IsKeyedService && x.ImplementationFactory?.GetType() == typeof(Func<IServiceProvider, SubscriptionHostedService>));
        services.Remove(host);

        configureServices?.Invoke(services);

        Provider = services.BuildServiceProvider();

        // Start hosted services (schema init)
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StartAsync(CancellationToken.None);
        }

        Provider.AddEventuousLogs();
        EventStore      = Provider.GetRequiredService<IEventStore>();
        CheckpointStore = Provider.GetRequiredService<ICheckpointStore>();
        Subscription    = Provider.GetRequiredService<TSubscription>();
        Handler         = Provider.GetRequiredKeyedService<TEventHandler>(SubscriptionId);
        LoggerFactory   = Provider.GetRequiredService<ILoggerFactory>();
        Log             = LoggerFactory.CreateLogger(GetType());

        if (autoStart) await StartSubscription();
    }

    internal ValueTask StartSubscription() => Subscription.SubscribeWithLog(Log);

    internal ValueTask StopSubscription() => Subscription.UnsubscribeWithLog(Log);

    /// <summary>
    /// Returns the subscription's end-of-stream measure delegate (requires an <see cref="IMeasuredSubscription"/>).
    /// </summary>
    internal GetSubscriptionEndOfStream GetMeasure() => ((IMeasuredSubscription)Subscription).GetMeasure();

    public async Task<ulong> GetLastPosition() {
        await using var connection = await ConnectionFactory.GetConnection(ConnectionString, default);
        await using var cmd        = connection.CreateCommand();
        cmd.CommandText = $"SELECT MAX(global_position) FROM {_schemaName}_messages";
        var result = await cmd.ExecuteScalarAsync();

        return (ulong)(result is DBNull or null ? 0 : (long)result);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) return;

        _disposed = true;

        try { await StopSubscription(); } catch {
            /* best effort */
        }

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
        try {
            if (File.Exists(path)) File.Delete(path);
        } catch {
            /* best effort */
        }
    }

    static string GetSchemaName() => NormaliseRegex().Replace(new Faker().Internet.UserName(), "").ToLower();

    [GeneratedRegex(@"[\.\-\s]")]
    private static partial Regex NormaliseRegex();

    bool _disposed;
}
