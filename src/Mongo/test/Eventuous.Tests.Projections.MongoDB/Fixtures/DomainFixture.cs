using Eventuous.Projections.MongoDB.Tools;
using NodaTime;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Projections.MongoDB.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes();

    public static BookingImported CreateImportBooking() {
        var from = Instance.Auto.Create<DateTime>();

        return new BookingImported(
            Instance.Auto.Create<string>(),
            Instance.Auto.Create<decimal>(),
            LocalDate.FromDateTime(from),
            LocalDate.FromDateTime(from.AddDays(Instance.Auto.Create<int>()))
        );
    }
}

public record BookingDocument(string Id) : ProjectedDocument(Id) {
    public string    GuestId      { get; init; } = null!;
    public string    RoomId       { get; init; } = null!;
    public LocalDate CheckInDate  { get; init; }
    public LocalDate CheckOutDate { get; init; }
    public decimal   BookingPrice { get; init; }
    public decimal   PaidAmount   { get; init; }
    public decimal   Outstanding  { get; init; }
    public bool      Paid         { get; init; }
}
