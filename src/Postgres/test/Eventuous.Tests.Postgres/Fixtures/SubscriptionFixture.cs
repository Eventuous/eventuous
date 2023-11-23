using Eventuous.Diagnostics.Logging;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Sut.Subs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Eventuous.Tests.Postgres.Fixtures;

public abstract class SubscriptionFixture<T> : IntegrationFixture where T : class, IEventHandler {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected StreamName              Stream          { get; }
    protected T                       Handler         { get; private set; } = null!;
    protected ILogger                 Log             { get; set; }         = null!;
    protected PostgresCheckpointStore CheckpointStore { get; private set; } = null!;
    IMessageSubscription              Subscription    { get; set; }         = null!;
    protected ILoggerFactory          LoggerFactory   { get; set; }         = null!;

    protected SubscriptionFixture(
            ITestOutputHelper outputHelper,
            bool              subscribeToAll,
            bool              autoStart = true,
            LogLevel          logLevel  = LogLevel.Trace
        ) {
        _outputHelper   = outputHelper;
        _subscribeToAll = subscribeToAll;
        _autoStart      = autoStart;
        _logLevel       = logLevel;
        Stream          = new StreamName(Auto.Create<string>());
        SubscriptionId  = $"test-{Guid.NewGuid():N}";
    }

    protected abstract T GetHandler();

    public string SubscriptionId { get; }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly ITestOutputHelper _outputHelper;
    readonly bool              _subscribeToAll;
    readonly bool              _autoStart;
    readonly LogLevel          _logLevel;
    LoggingEventListener       _listener;

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);

        services.AddCheckpointStore<PostgresCheckpointStore>(
            sp
                => new PostgresCheckpointStore(sp.GetRequiredService<NpgsqlDataSource>(), SchemaName, sp.GetService<ILoggerFactory>())
        );

        if (_subscribeToAll) {
            services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
                SubscriptionId,
                b => {
                    b.AddEventHandler(_ => GetHandler());
                    b.Configure(options => options.Schema = SchemaName);
                }
            );

            services.AddSingleton<IMessageSubscription>(
                sp => sp.GetSubscriptionBuilder<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(SubscriptionId).ResolveSubscription(sp)
            );
        }
        else {
            services.AddSubscription<PostgresStreamSubscription, PostgresStreamSubscriptionOptions>(
                SubscriptionId,
                b => {
                    b.AddEventHandler(_ => GetHandler());

                    b.Configure(
                        options => {
                            options.Stream = Stream;
                            options.Schema = SchemaName;
                        }
                    );
                }
            );

            services.AddSingleton<IMessageSubscription>(
                sp => sp.GetSubscriptionBuilder<PostgresStreamSubscription, PostgresStreamSubscriptionOptions>(SubscriptionId).ResolveSubscription(sp)
            );
        }

        var host = services.First(x => x.ImplementationFactory?.GetType() == typeof(Func<IServiceProvider, SubscriptionHostedService>));
        services.Remove(host);
        services.AddLogging(b => b.AddXunit(_outputHelper, _logLevel).SetMinimumLevel(_logLevel));
    }

    protected override void GetDependencies(IServiceProvider provider) {
        provider.AddEventuousLogs();
        base.GetDependencies(provider);
        CheckpointStore = provider.GetRequiredService<PostgresCheckpointStore>();
        Subscription    = provider.GetRequiredService<IMessageSubscription>();
        Handler         = provider.GetRequiredService<T>();
        LoggerFactory   = provider.GetRequiredService<ILoggerFactory>();
        Log             = LoggerFactory.CreateLogger(GetType());
        _listener       = new LoggingEventListener(LoggerFactory);
    }

    public override async Task InitializeAsync() {
        await base.InitializeAsync();
        if (_autoStart) await Start();
    }

    public override async Task DisposeAsync() {
        if (_autoStart) await Stop();
        _listener.Dispose();
        await base.DisposeAsync();
    }
}
