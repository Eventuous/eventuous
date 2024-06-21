// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.RegularExpressions;
using Bogus;
using DotNet.Testcontainers.Containers;
using MicroElements.AutoFixture.NodaTime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.Persistence.Base.Fixtures;

public abstract class StoreFixtureBase {
    public           IEventStore        EventStore     { get; protected private set; } = null!;
    public           IAggregateStore    AggregateStore { get; protected private set; } = null!;
    public           IFixture           Auto           { get; }                        = new Fixture().Customize(new NodaTimeCustomization());
    protected static Faker              Faker          { get; }                        = new();
    protected        ServiceProvider    Provider       { get; set; }                   = null!;
    protected        bool               AutoStart      { get; init; }                  = true;
    public           ITestOutputHelper? Output         { get; set; }
}

public abstract partial class StoreFixtureBase<TContainer> : StoreFixtureBase, IAsyncLifetime where TContainer : DockerContainer {
    IEventSerializer Serializer { get; } =
        new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));

    public virtual async Task InitializeAsync() {
        Container = CreateContainer();
        await Container.StartAsync();

        var services = new ServiceCollection();

        if (Output != null) {
            services.AddSingleton(Output);
            services.AddLogging(cfg => cfg.AddXunit(Output, LogLevel.Debug).SetMinimumLevel(LogLevel.Debug));
        }

        SetupServices(services);

        Provider = services.BuildServiceProvider();

        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = Provider.GetRequiredService<IEventStore>();
        AggregateStore = Provider.GetRequiredService<IAggregateStore>();
        GetDependencies(Provider);

        if (AutoStart) {
            await Start();
        }
    }

    protected async Task Start() {
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StartAsync(default);
        }
    }

    public virtual async Task DisposeAsync() {
        if (_disposed) return;

        _disposed = true;
        var inits = Provider.GetServices<IHostedService>();

        foreach (var hostedService in inits) {
            await hostedService.StopAsync(default);
        }

        await Provider.DisposeAsync();
        await Container.DisposeAsync();
    }

    protected abstract void SetupServices(IServiceCollection services);

    protected abstract TContainer CreateContainer();

    protected virtual void GetDependencies(IServiceProvider provider) { }

    protected TContainer Container { get; private set; } = null!;

    bool _disposed;

    public static string GetSchemaName() => NormaliseRegex().Replace(new Faker().Internet.UserName(), "").ToLower();

#if NET8_0_OR_GREATER
    [GeneratedRegex(@"[\.\-\s]")]
    private static partial Regex NormaliseRegex();
#else
    static Regex NormaliseRegex() => new(@"[\.\-\s]");
#endif
}
