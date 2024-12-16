using Bogus;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);

    static Faker<Commands.ImportBooking> Faker => new Faker<Commands.ImportBooking>()
        .RuleFor(x => x.BookingId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.RoomId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.Price, f => f.Random.Number(50, 200))
        .RuleFor(x => x.CheckIn, f => f.Noda().LocalDate.Soon())
        .RuleFor(x => x.CheckOut, (f, c) => c.CheckIn.PlusDays(f.Random.Number(1, 5)));

    public static Commands.ImportBooking CreateImportBooking() => Faker.Generate();
}
