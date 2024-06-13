using Eventuous.Projections.MongoDB.Tools;
using NodaTime;

namespace Bookings.Application.Queries;

public record BookingDocument : ProjectedDocument {
    public BookingDocument(string id) : base(id) { }

    public string    GuestId      { get; init; }
    public string    RoomId       { get; init; }
    public LocalDate CheckInDate  { get; init; }
    public LocalDate CheckOutDate { get; init; }
    public float     BookingPrice { get; init; }
    public float     PaidAmount   { get; init; }
    public float     Outstanding  { get; init; }
    public bool      Paid         { get; init; }
}