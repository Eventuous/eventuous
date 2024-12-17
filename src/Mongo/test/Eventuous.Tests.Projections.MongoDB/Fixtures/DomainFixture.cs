using Bogus;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using NodaTime;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);

    static Faker<Commands.ImportBooking> CmdFaker => new Faker<Commands.ImportBooking>()
        .RuleFor(x => x.BookingId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.RoomId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.Price, f => f.Random.Number(50, 200))
        .RuleFor(x => x.CheckIn, f => f.Noda().LocalDate.Soon())
        .RuleFor(x => x.CheckOut, (f, c) => c.CheckIn.PlusDays(f.Random.Number(1, 5)));
    
    static Faker<BookingEvents.BookingImported> EventFaker => new Faker<BookingEvents.BookingImported>()
        .CustomInstantiator(f => {
                var checkIn = f.Noda().LocalDate.Soon();
                return new(f.Commerce.Product(), f.Random.Number(50, 200), checkIn, checkIn.PlusDays(f.Random.Number(1, 5)));
            }
        );

    public static Commands.ImportBooking CreateImportBooking() => CmdFaker.Generate();
    
    public static BookingEvents.BookingImported CreateImportBookingEvent() => EventFaker.Generate();
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
