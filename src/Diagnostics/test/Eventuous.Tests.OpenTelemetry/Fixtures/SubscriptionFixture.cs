// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;
using Eventuous.EventStore.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.OpenTelemetry.Fixtures;

public class SubscriptionFixture : IntegrationFixture {
    public const int Count = 100;

    static SubscriptionFixture() {
        TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
        EventuousDiagnostics.AddDefaultTag("test", "foo");
    }

    public StreamName Stream { get; } = new($"test-{Guid.NewGuid():N}");

    public override async Task InitializeAsync() {
        await base.InitializeAsync();

        var builder = new WebHostBuilder()
            .Configure(_ => { })
            .ConfigureServices(
                services => {
                    services.AddSingleton(Client);
                    services.AddEventProducer<EventStoreProducer>();
                }
            );
        using var host = new TestServer(builder);

        var testEvents = Auto.CreateMany<TestEvent>(Count).ToList();
        var producer   = host.Services.GetRequiredService<IEventProducer>();
        await producer.Produce(Stream, testEvents, new Metadata());
    }
}
