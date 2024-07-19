namespace Eventuous.Tests.Fixtures;

using Sut.App;
using Testing;

public class NaiveFixture {
    protected IEventStore EventStore { get; } = new InMemoryEventStore();
    protected Fixture     Auto       { get; } = new();

    protected Commands.BookRoom CreateBookRoomCommand() => new(
        Auto.Create<string>(),
        Auto.Create<string>(),
        LocalDate.FromDateTime(DateTime.Today),
        LocalDate.FromDateTime(DateTime.Today.AddDays(2)),
        Auto.Create<float>()
    );
}
