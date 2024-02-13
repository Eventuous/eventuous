using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscriptionTestBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, T> : StoreFixtureBase<TContainer>
    where T : class, IEventHandler
    where TContainer : DockerContainer
    where TCheckpointStore : class, ICheckpointStore
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions {
    static SubscriptionTestBase() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected StreamName       Stream          { get; }
    protected T                Handler         { get; private set; } = null!;
    protected ILogger          Log             { get; set; }         = null!;
    protected ICheckpointStore CheckpointStore { get; private set; } = null!;
    IMessageSubscription       Subscription    { get; set; }         = null!;
    protected ILoggerFactory   LoggerFactory   { get; set; }         = null!;

    protected SubscriptionTestBase(ITestOutputHelper outputHelper, bool autoStart = true, LogLevel logLevel = LogLevel.Trace) {
        _outputHelper  = outputHelper;
        _autoStart     = autoStart;
        _logLevel      = logLevel;
        Stream         = new StreamName(Auto.Create<string>());
        SubscriptionId = $"test-{Guid.NewGuid():N}";
    }

    protected abstract T GetHandler();

    public string SubscriptionId { get; }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly ITestOutputHelper _outputHelper;
    readonly bool              _autoStart;
    readonly LogLevel          _logLevel;

    protected abstract TCheckpointStore GetCheckpointStore(IServiceProvider sp);

    protected abstract void ConfigureSubscription(TSubscriptionOptions options);

    protected override void SetupServices(IServiceCollection services) {
        services.AddCheckpointStore(GetCheckpointStore);

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            SubscriptionId,
            b => {
                b.AddEventHandler(_ => GetHandler());
                b.Configure(ConfigureSubscription);
            }
        );

        services.AddSingleton<IMessageSubscription>(
            sp => sp.GetSubscriptionBuilder<TSubscription, TSubscriptionOptions>(SubscriptionId).ResolveSubscription(sp)
        );

        var host = services.First(x => x.ImplementationFactory?.GetType() == typeof(Func<IServiceProvider, SubscriptionHostedService>));
        services.Remove(host);
        services.AddLogging(b => b.AddXunit(_outputHelper, _logLevel).SetMinimumLevel(_logLevel));
    }

    protected override void GetDependencies(IServiceProvider provider) {
        provider.AddEventuousLogs();
        base.GetDependencies(provider);
        CheckpointStore = provider.GetRequiredService<ICheckpointStore>();
        Subscription    = provider.GetRequiredService<IMessageSubscription>();
        Handler         = provider.GetRequiredService<T>();
        LoggerFactory   = provider.GetRequiredService<ILoggerFactory>();
        Log             = LoggerFactory.CreateLogger(GetType());
    }

    public override async Task InitializeAsync() {
        await base.InitializeAsync();
        if (_autoStart) await Start();
    }

    public override async Task DisposeAsync() {
        if (_autoStart) await Stop();
        await base.DisposeAsync();
    }
}
