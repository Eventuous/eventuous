using Eventuous.Sut.App;
using Eventuous.TestHelpers.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Eventuous.Tests.Fixtures;

public class NaiveFixture {
    protected IEventStore     EventStore     { get; }
    protected IAggregateStore AggregateStore { get; }
    protected Fixture         Auto           { get; } = new();

    protected NaiveFixture() {
        EventStore     = new InMemoryEventStore();
        AggregateStore = new AggregateStore(EventStore, memoryCache: new MemoryCache(Options.Create<MemoryCacheOptions>(new())));
    }

    protected Commands.BookRoom CreateBookRoomCommand() => new(
        Auto.Create<string>(),
        Auto.Create<string>(),
        LocalDate.FromDateTime(DateTime.Today),
        LocalDate.FromDateTime(DateTime.Today.AddDays(2)),
        Auto.Create<float>()
    );
}