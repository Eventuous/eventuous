using System.Text.Json;
using Eventuous.TestHelpers;
using Eventuous.TestHelpers.TUnit.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using RestSharp.Serializers.Json;

namespace Eventuous.Tests.Extensions.AspNetCore.Fixture;

using static SutBookingCommands;

public class ServerFixture {
    readonly AutoFixture.Fixture _fixture = new();

    public ServerFixture(
            WebApplicationFactory<Program> factory,
            Action<IServiceCollection>?    register  = null,
            ConfigureWebApplication?       configure = null
        ) {
        var builder = factory
            .WithWebHostBuilder(
                builder => {
                    builder
                        .ConfigureServices(
                            services => {
                                register?.Invoke(services);
                                if (configure != null) services.AddSingleton(configure);
                            }
                        )
                        .ConfigureLogging(x => x.ForTests());
                }
            );
        builder.Server.PreserveExecutionContext = false;

        _app = builder;
    }

    readonly JsonSerializerOptions          _options = TestPrimitives.DefaultOptions;
    readonly WebApplicationFactory<Program> _app;

    public RestClient GetClient() {
        return new(
            _app.CreateClient(),
            disposeHttpClient: true,
            configureSerialization: s => s.UseSerializer(() => new SystemTextJsonSerializer(_options))
        );
    }

    public T Resolve<T>() where T : notnull => _app.Services.GetRequiredService<T>();

    public Task<StreamEvent[]> ReadStream<T>(string id)
        => Resolve<IEventStore>().ReadEvents(StreamName.For<T>(id), StreamReadPosition.Start, 100, default);

    internal BookRoom GetBookRoom() {
        var now  = new DateTime(2023, 10, 1);
        var date = LocalDate.FromDateTime(now);

        return new(_fixture.Create<string>(), _fixture.Create<string>(), date, date.PlusDays(1), 100, "guest");
    }

    internal NestedCommands.NestedBookRoom GetNestedBookRoom(DateTime? dateTime = null) {
        var date = LocalDate.FromDateTime(dateTime ?? DateTime.Now);

        return new(_fixture.Create<string>(), _fixture.Create<string>(), date, date.PlusDays(1), 100, "guest");
    }

    public async Task<string> ExecuteRequest<TCommand, TResult>(TCommand cmd, string route, string id)
        where TCommand : class where TResult : State<TResult>, new() {
        using var client = GetClient();

        var request  = new RestRequest(route).AddJsonBody(cmd);
        var response = await client.ExecutePostAsync<Result<TResult>.Ok>(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return response.Content!;
    }
}
