using NodaTime;

// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Bookings.Application.Queries;

/// <summary>
/// Booking state projected to Azure Blob Storage, one blob per booking stream.
/// Requires a parameterless constructor, as the blob projector creates a new instance for new blobs.
/// </summary>
public record BookingView {
    public string    Id           { get; init; } = "";
    public string    GuestId      { get; init; } = "";
    public string    RoomId       { get; init; } = "";
    public LocalDate CheckInDate  { get; init; }
    public LocalDate CheckOutDate { get; init; }
    public float     BookingPrice { get; init; }
    public float     Outstanding  { get; init; }
    public bool      Paid         { get; init; }
}
