// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using DotNet.Testcontainers.Containers;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Eventuous.Sut.Subs;
using Eventuous.Tests.OpenTelemetry.Fakes;
using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.OpenTelemetry.Fixtures;

public abstract class MetricsSubscriptionFixtureBase<TContainer, TProducer, TSubscription, TSubscriptionOptions>
    : StoreFixtureBase<TContainer>
    where TContainer : DockerContainer
    where TProducer : class, IEventProducer
    where TSubscription : EventSubscriptionWithCheckpoint<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionWithCheckpointOptions {
    // ReSharper disable once ConvertToConstant.Global
    public readonly int Count = 100;

    static MetricsSubscriptionFixtureBase() {
        TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
        EventuousDiagnostics.AddDefaultTag("test", "foo");
    }

    public StreamName Stream { get; } = new($"test-{Guid.NewGuid():N}");

    // ReSharper disable once ConvertToConstant.Global
    public readonly string SubscriptionId = "test-sub";

    protected abstract void ConfigureSubscription(TSubscriptionOptions options);

    protected override void SetupServices(IServiceCollection services) {
        services.AddProducer<TProducer>();
        services.AddSingleton<MessageCounter>();

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            SubscriptionId,
            builder => builder
                .Configure(ConfigureSubscription)
                .UseCheckpointStore<TSubscription, TSubscriptionOptions, NoOpCheckpointStore>()
                .AddEventHandler<TestHandler>()
        );

        services.AddOpenTelemetry()
            .WithMetrics(
                builder => builder
                    .AddEventuousSubscriptions()
                    .AddReader(new BaseExportingMetricReader(Exporter))
            );
    }

    protected override void GetDependencies(IServiceProvider provider) {
        Producer = provider.GetRequiredService<TProducer>();
        Counter  = provider.GetRequiredService<MessageCounter>();
    }

    public TProducer Producer { get; private set; } = null!;

    public TestExporter   Exporter { get; }              = new();
    public MessageCounter Counter  { get; private set; } = null!;

    public override async Task DisposeAsync() {
        await base.DisposeAsync();
        Exporter.Dispose();
    }
}