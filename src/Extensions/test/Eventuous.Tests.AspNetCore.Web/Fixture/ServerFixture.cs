// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Eventuous.TestHelpers.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using NodaTime.Serialization.SystemTextJson;
using RestSharp.Serializers.Json;

namespace Eventuous.Tests.AspNetCore.Web.Fixture;

using static SutBookingCommands;

public class ServerFixture : IDisposable {
    readonly WebApplicationFactory<Program> _app;
    readonly AutoFixture.Fixture            _fixture = new();

    public ServerFixture(ITestOutputHelper output, Action<IServiceCollection>? register = null, ConfigureWebApplication? configure = null) {
        TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);

        Store = new InMemoryEventStore();

        _app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(
                builder => builder
                    .ConfigureServices(
                        services => {
                            register?.Invoke(services);
                            services.AddAggregateStore(_ => Store);
                            if (configure != null) services.AddSingleton(configure);
                        }
                    )
                    .ConfigureLogging(x => x.AddXunit(output).AddConsole().SetMinimumLevel(LogLevel.Debug))
            );
    }

    InMemoryEventStore Store { get; }

    static readonly JsonSerializerOptions Options = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    public RestClient GetClient()
        => new(
            _app.CreateClient(),
            disposeHttpClient: true,
            configureSerialization: s => s.UseSerializer(() => new SystemTextJsonSerializer(Options))
        );

    public T Resolve<T>() where T : notnull
        => _app.Services.GetRequiredService<T>();

    public Task<StreamEvent[]> ReadStream<T>(string id)
        => Store.ReadEvents(StreamName.For<T>(id), StreamReadPosition.Start, 100, default);

    internal BookRoom GetBookRoom() {
        var date = LocalDate.FromDateTime(DateTime.Now);

        return new(_fixture.Create<string>(), _fixture.Create<string>(), date, date.PlusDays(1), 100, "guest");
    }

    internal NestedCommands.NestedBookRoom GetNestedBookRoom(DateTime? dateTime = null) {
        var date = LocalDate.FromDateTime(dateTime ?? DateTime.Now);

        return new(_fixture.Create<string>(), _fixture.Create<string>(), date, date.PlusDays(1), 100, "guest");
    }

    public async Task ExecuteAndVerify<TCommand, TAggregate>(TCommand cmd, string route, string id, params object[] expected) where TCommand : class {
        var events = await ExecuteAndRead<TCommand, TAggregate>(cmd, route, id);
        var last   = events.TakeLast(expected.Length).Select(x => x.Payload);
        last.Should().BeEquivalentTo(expected);
    }

    public async Task<StreamEvent[]> ExecuteAndRead<TCommand, TAggregate>(TCommand cmd, string route, string id) where TCommand : class {
        using var client = GetClient();

        var request  = new RestRequest(route).AddJsonBody(cmd);
        var response = await client.ExecutePostAsync<OkResult>(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await ReadStream<TAggregate>(id);
    }

    public void Dispose() => _app.Dispose();
}
