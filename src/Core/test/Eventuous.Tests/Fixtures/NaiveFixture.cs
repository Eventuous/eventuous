using Eventuous.Sut.App;
using Eventuous.TestHelpers.Fakes;

namespace Eventuous.Tests.Fixtures;

public class NaiveFixture {
    protected IEventStore     EventStore     { get; }
    protected IAggregateStore AggregateStore { get; }
    protected Fixture         Auto           { get; } = new();

    protected NaiveFixture() {
        EventStore     = new InMemoryEventStore();
        AggregateStore = new AggregateStore(EventStore);
    }

    protected Commands.BookRoom CreateBookRoomCommand() => new(
        Auto.Create<string>(),
        Auto.Create<string>(),
        LocalDate.FromDateTime(DateTime.Today),
        LocalDate.FromDateTime(DateTime.Today.AddDays(2)),
        Auto.Create<float>()
    );
}