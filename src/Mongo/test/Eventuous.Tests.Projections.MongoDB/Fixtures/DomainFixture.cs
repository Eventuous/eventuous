using Bogus;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.App;
using NodaTime;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes();

    static Faker<Commands.ImportBooking> Faker => new Faker<Commands.ImportBooking>()
        .RuleFor(x => x.BookingId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.RoomId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.Price, f => f.Random.Number(50, 200))
        .RuleFor(x => x.CheckIn, f => f.Noda().LocalDate.Soon())
        .RuleFor(x => x.CheckOut, (f, c) => c.CheckIn.PlusDays(f.Random.Number(1, 5)));

    public static Commands.ImportBooking CreateImportBooking() => Faker.Generate();
}

public record BookingDocument(string Id) : ProjectedDocument(Id) {
    public string    GuestId      { get; init; } = null!;
    public string    RoomId       { get; init; } = null!;
    public LocalDate CheckInDate  { get; init; }
    public LocalDate CheckOutDate { get; init; }
    public float     BookingPrice { get; init; }
    public float     PaidAmount   { get; init; }
    public float     Outstanding  { get; init; }
    public bool      Paid         { get; init; }
}
