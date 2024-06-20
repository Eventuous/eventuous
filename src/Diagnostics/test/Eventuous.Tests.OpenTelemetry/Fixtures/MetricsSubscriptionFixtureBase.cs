// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using DotNet.Testcontainers.Containers;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Eventuous.Tests.OpenTelemetry.Fakes;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.OpenTelemetry.Fixtures;

public abstract class MetricsSubscriptionFixtureBase<TContainer, TProducer, TSubscription, TSubscriptionOptions> : StoreFixtureBase<TContainer>
    where TContainer : DockerContainer
    where TProducer : class, IEventProducer
    where TSubscription : EventSubscriptionWithCheckpoint<TSubscriptionOptions>, IMeasuredSubscription
    where TSubscriptionOptions : SubscriptionWithCheckpointOptions {
    // ReSharper disable once ConvertToConstant.Global
    public readonly int Count = 100;

    // ReSharper disable once StaticMemberInGenericType
    static readonly KeyValuePair<string, string> DefaultTag = new("test", "foo");

    static MetricsSubscriptionFixtureBase() {
        TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
        EventuousDiagnostics.AddDefaultTag(DefaultTag.Key, DefaultTag.Value);
    }

    public StreamName Stream          { get; } = new($"test-{Guid.NewGuid():N}");
    public string     DefaultTagKey   => DefaultTag.Key;
    public string     DefaultTagValue => DefaultTag.Value;

    // ReSharper disable once ConvertToConstant.Global
    public readonly string SubscriptionId = "test-sub";
    TestListener?          _listener;

    protected abstract void ConfigureSubscription(TSubscriptionOptions options);

    protected override void SetupServices(IServiceCollection services) {
        if (Output != null) {
            _listener = new(Output);
        }

        services.AddProducer<TProducer>();
        services.AddSingleton<MessageCounter>();

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            SubscriptionId,
            builder => builder
                .Configure(ConfigureSubscription)
                .UseCheckpointStore<TSubscription, TSubscriptionOptions, NoOpCheckpointStore>()
                .AddEventHandler<TestHandler>()
        );

        services.AddOpenTelemetry().WithMetrics(builder => builder.AddEventuousSubscriptions().AddReader(new BaseExportingMetricReader(Exporter)));
    }

    protected override void GetDependencies(IServiceProvider provider) {
        provider.AddEventuousLogs();
        Producer = provider.GetRequiredService<TProducer>();
        Counter  = provider.GetRequiredService<MessageCounter>();
    }

    public TProducer      Producer { get; private set; } = null!;
    public MessageCounter Counter  { get; private set; } = null!;
    public TestExporter   Exporter { get; }              = new();

    public override async Task DisposeAsync() {
        await base.DisposeAsync();
        Exporter.Dispose();
        _listener?.Dispose();
    }
}

class TestListener(ITestOutputHelper output) : GenericListener(SubscriptionMetrics.ListenerName) {
    protected override void OnEvent(KeyValuePair<string, object?> obj) => output.WriteLine($"{obj.Key} {obj.Value}");
}
