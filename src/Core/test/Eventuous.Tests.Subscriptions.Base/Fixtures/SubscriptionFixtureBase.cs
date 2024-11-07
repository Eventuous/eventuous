using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TEventHandler>
    : StoreFixtureBase<TContainer>, IStartableFixture
    where TEventHandler : class, IEventHandler
    where TContainer : DockerContainer
    where TCheckpointStore : class, ICheckpointStore
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions {
    readonly bool     _autoStart;
    readonly LogLevel _logLevel;

    protected SubscriptionFixtureBase(bool autoStart = true, LogLevel logLevel = LogLevel.Trace) {
        _autoStart = autoStart;
        _logLevel  = logLevel;
        TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
    }

    protected internal TEventHandler    Handler         { get; private set; } = null!;
    protected          ILogger          Log             { get; set; }         = null!;
    protected internal ICheckpointStore CheckpointStore { get; private set; } = null!;
    IMessageSubscription                Subscription    { get; set; }         = null!;
    protected internal ILoggerFactory   LoggerFactory   { get; set; }         = null!;

    public string SubscriptionId { get; } = $"test-{Guid.NewGuid():N}";

    protected internal ValueTask StartSubscription() => Subscription.SubscribeWithLog(Log);

    protected internal ValueTask StopSubscription() => Subscription.UnsubscribeWithLog(Log);

    protected abstract TCheckpointStore GetCheckpointStore(IServiceProvider sp);

    protected abstract void ConfigureSubscription(TSubscriptionOptions options);

    protected override void SetupServices(IServiceCollection services) {
        services.AddCheckpointStore(GetCheckpointStore);

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            SubscriptionId,
            b => {
                b.AddEventHandler<TEventHandler>();
                b.Configure(ConfigureSubscription);
            }
        );

        services.AddSingleton<IMessageSubscription>(
            sp => sp.GetSubscriptionBuilder<TSubscription, TSubscriptionOptions>(SubscriptionId).ResolveSubscription(sp)
        );

        var host = services.First(x => !x.IsKeyedService && x.ImplementationFactory?.GetType() == typeof(Func<IServiceProvider, SubscriptionHostedService>));
        services.Remove(host);
        services.AddLogging(b => ConfigureLogging(b.ForTests(_logLevel)));
    }

    protected override void GetDependencies(IServiceProvider provider) {
        provider.AddEventuousLogs();
        base.GetDependencies(provider);
        CheckpointStore = provider.GetRequiredService<ICheckpointStore>();
        Subscription    = provider.GetRequiredService<IMessageSubscription>();
        Handler         = provider.GetRequiredKeyedService<TEventHandler>(SubscriptionId);
        LoggerFactory   = provider.GetRequiredService<ILoggerFactory>();
        Log             = LoggerFactory.CreateLogger(GetType());
    }

    protected virtual ILoggingBuilder ConfigureLogging(ILoggingBuilder builder) => builder;

    public abstract Task<ulong> GetLastPosition();

    public override async Task InitializeAsync() {
        await base.InitializeAsync();
        if (_autoStart) await StartSubscription();
    }

    public override async ValueTask DisposeAsync() {
        if (_autoStart) await StopSubscription();
        await base.DisposeAsync();
    }
}
