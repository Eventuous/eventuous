// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Eventuous.Sut.AspNetCore;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using RestSharp;
using RestSharp.Serializers.Json;

namespace Eventuous.Tests.AspNetCore.Web.Fixture;

public class ServerFixture : IDisposable {
    readonly WebApplicationFactory<Program> _app;
    readonly AutoFixture.Fixture            _fixture = new();

    public ServerFixture(Action<IServiceCollection>? register = null, ConfigureWebApplication? configure = null) {
        TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);

        Store = new InMemoryEventStore();

        _app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(
                builder => builder.ConfigureServices(services => {
                        register?.Invoke(services);
                        services.AddAggregateStore(_ => Store);
                        if (configure != null) services.AddSingleton(configure);
                    }
                )
            );
    }

    InMemoryEventStore Store { get; }

    public RestClient GetClient()
        => new RestClient(_app.CreateClient(), disposeHttpClient: true).UseSerializer(
            () => new SystemTextJsonSerializer(
                new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
            )
        );

    public T Resolve<T>() where T : notnull
        => _app.Services.GetRequiredService<T>();

    public Task<StreamEvent[]> ReadStream<T>(string id)
        => Store.ReadEvents(
            StreamName.For<T>(id),
            StreamReadPosition.Start,
            100,
            default
        );

    internal BookRoom GetBookRoom() {
        var date = LocalDate.FromDateTime(DateTime.Now);
        return new(_fixture.Create<string>(), _fixture.Create<string>(), date, date.PlusDays(1), 100);
    }

    public void Dispose()
        => _app.Dispose();
}

record BookRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price);
