using Azure.Storage.Blobs;
using Eventuous.Azure.Storage.Blobs;
using Eventuous.Subscriptions.Context;
using static Bookings.Domain.Bookings.BookingEvents;

namespace Bookings.Application.Queries;

public class MyBookingsProjection : StorageBlobsProjector<MyBookings> {
    public MyBookingsProjection(BlobServiceClient client) : base(client, "bookings-container") {
        On<V1.RoomBooked>(AddBooking);

        On<V1.BookingCancelled>(CancelBooking);
    }

    private static MyBookings AddBooking(IMessageConsumeContext<V1.RoomBooked> ctx, MyBookings b) => b with {
        Bookings = b.Bookings.Add(new(ctx.Stream.GetId(), ctx.Message.CheckInDate, ctx.Message.CheckOutDate, ctx.Message.BookingPrice))
    };

    private static MyBookings CancelBooking(IMessageConsumeContext<V1.BookingCancelled> ctx, MyBookings b) => b with {
        Bookings = b.Bookings.RemoveAll(booking => booking.BookingId == ctx.Stream.GetId())
    };
}
