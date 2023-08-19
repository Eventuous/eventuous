using Eventuous.Projections.MongoDB.Tools;
using NodaTime;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public static class DomainFixture {
    static Fixture Auto { get; } = new();

    static DomainFixture()
        => TypeMap.RegisterKnownEventTypes();

    public static BookingImported CreateImportBooking() {
        var from = Auto.Create<DateTime>();

        return new BookingImported(
            Auto.Create<string>(),
            Auto.Create<float>(),
            LocalDate.FromDateTime(from),
            LocalDate.FromDateTime(from.AddDays(Auto.Create<int>()))
        );
    }
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
