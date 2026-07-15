using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TEventHandler> : StoreFixtureBase<TContainer>
    where TEventHandler : class, IEventHandler
    where TContainer : DockerContainer
    where TCheckpointStore : class, ICheckpointStore
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions {
    readonly bool     _autoStart;

    protected SubscriptionFixtureBase(bool autoStart = true, LogLevel logLevel = LogLevel.Information) : base(logLevel) {
        _autoStart = autoStart;
        TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
    }

    protected internal TEventHandler    Handler         { get; private set; } = null!;
    protected          ILogger          Log             { get; set; }         = null!;
    protected internal ICheckpointStore CheckpointStore { get; private set; } = null!;
    IMessageSubscription                Subscription    { get; set; }         = null!;
    protected internal ILoggerFactory   LoggerFactory   { get; set; }         = null!;

    /// <summary>
    /// Health check fed by the subscription's subscribed/dropped callbacks. Lets tests assert that a dropped
    /// subscription reports unhealthy and that it recovers to healthy after resubscription.
    /// </summary>
    protected internal SubscriptionHealthCheck Health { get; } = new();

    /// <summary>
    /// True when the subscription has detected a drop and is trying to resubscribe.
    /// </summary>
    public bool IsDropped => ((EventSubscription<TSubscriptionOptions>)Subscription).IsDropped;

    /// <summary>
    /// Returns the subscription's end-of-stream measure delegate (requires an <see cref="IMeasuredSubscription"/>).
    /// </summary>
    protected internal GetSubscriptionEndOfStream GetMeasure() => ((IMeasuredSubscription)Subscription).GetMeasure();

    public string SubscriptionId { get; } = $"test-{Guid.NewGuid():N}";

    protected internal ValueTask StartSubscription()
        => Subscription.Subscribe(
            id => {
                Health.ReportHealthy(id);
                Log.LogInformation("{Subscription} subscribed", id);
            },
            (id, reason, ex) => {
                Health.ReportUnhealthy(id, ex);
                Log.LogWarning(ex, "{Subscription} dropped {Reason}", id, reason);
            },
            CancellationToken.None
        );

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

        services.AddSingleton<IMessageSubscription>(sp => sp.GetSubscriptionBuilder<TSubscription, TSubscriptionOptions>(SubscriptionId).ResolveSubscription(sp));

        var host = services.First(x => !x.IsKeyedService && x.ImplementationFactory?.GetType() == typeof(Func<IServiceProvider, SubscriptionHostedService>));
        services.Remove(host);
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
