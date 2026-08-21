using Bogus;

namespace Eventuous.Tests.Fixtures;

using Sut.App;
using Testing;

public class NaiveFixture {
    protected IEventStore EventStore { get; } = new InMemoryEventStore();

    // A fresh Faker per call rather than one shared instance: f.Noda() caches its dataset in that
    // Faker's Bogus.Premium context, a plain Dictionary, so tests running in parallel would race
    // its first write and corrupt it. Same shape as DomainFixture in the other test suites.
    static Faker<Commands.BookRoom> Faker => new Faker<Commands.BookRoom>()
        .CustomInstantiator(
            f => {
                var checkin  = f.Noda().LocalDate.Soon();
                var checkout = checkin.PlusDays(f.Random.Number(1, 5));

                return new(f.Random.Guid().ToString("N"), f.Random.Guid().ToString("N"), checkin, checkout, f.Random.Number(50, 200));
            }
        );

    protected static Commands.BookRoom CreateBookRoomCommand() => Faker.Generate();
}
