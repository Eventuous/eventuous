using Azure.Storage.Blobs;
using Eventuous.Azure.Storage.Blobs;
using static Bookings.Domain.Bookings.BookingEvents;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Bookings.Application.Queries;

public class BookingStateProjection : StorageBlobsProjector<BookingDocument> {
    public BookingStateProjection(BlobServiceClient client) : base(client, "bookings-container") {
        On<V1.RoomBooked>(HandleRoomBooked);

        On<V1.PaymentRecorded>((b, evt) => b with { Outstanding = evt.Outstanding });

        On<V1.BookingFullyPaid>((b, evt) => b with { Paid = true });
    }

    static BookingDocument HandleRoomBooked(BookingDocument bookingDocument, V1.RoomBooked evt) =>
        bookingDocument with {
            GuestId      = evt.GuestId,
            RoomId       = evt.RoomId,
            CheckInDate  = evt.CheckInDate,
            CheckOutDate = evt.CheckOutDate,
            BookingPrice = evt.BookingPrice,
            Outstanding  = evt.OutstandingAmount
        };
}
