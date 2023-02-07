// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using RestSharp;
using RestSharp.Serializers.Json;

namespace Eventuous.Tests.AspNetCore.Web.Fixture;

public class ServerFixture {
    readonly WebApplicationFactory<Program> _app;

    public ServerFixture() {
        TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);

        Store = new InMemoryEventStore();

        _app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(
                builder => builder.ConfigureServices(services => services.AddAggregateStore(_ => Store))
            );
    }

    public InMemoryEventStore Store { get; }

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
}
