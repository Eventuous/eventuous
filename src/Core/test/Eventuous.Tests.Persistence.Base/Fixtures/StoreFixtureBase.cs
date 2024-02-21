// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Text.Json;
using Bogus;
using DotNet.Testcontainers.Containers;
using Eventuous.Diagnostics;
using MicroElements.AutoFixture.NodaTime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.Persistence.Base.Fixtures;

public abstract class StoreFixtureBase {
    public        IEventStore     EventStore     { get; protected private set; } = null!;
    public        IAggregateStore AggregateStore { get; protected private set; } = null!;
    public        IFixture        Auto           { get; }                        = new Fixture().Customize(new NodaTimeCustomization());
    public static Faker           Faker          { get; }                        = new();
    public        ServiceProvider Provider       { get; set; }                   = null!;
}

public abstract class StoreFixtureBase<TContainer> : StoreFixtureBase, IAsyncLifetime where TContainer : DockerContainer {
    readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } =
        new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));

    public virtual async Task InitializeAsync() {
        Container = CreateContainer();
        await Container.StartAsync();

        var services = new ServiceCollection();
        SetupServices(services);

        Provider = services.BuildServiceProvider();
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = Provider.GetRequiredService<IEventStore>();
        AggregateStore = Provider.GetRequiredService<IAggregateStore>();
        GetDependencies(Provider);
        ActivitySource.AddActivityListener(_listener);
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StartAsync(default);
        }
    }

    public virtual async Task DisposeAsync() {
        await Container.DisposeAsync();
        _listener.Dispose();
    }

    protected abstract void SetupServices(IServiceCollection services);

    protected abstract TContainer CreateContainer();

    protected virtual void GetDependencies(IServiceProvider provider) { }

    protected TContainer Container { get; private set; } = null!;
}
