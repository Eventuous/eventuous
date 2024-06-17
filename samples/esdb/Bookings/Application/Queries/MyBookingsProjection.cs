using Eventuous.Projections.MongoDB;
using MongoDB.Driver;
using static Bookings.Domain.Bookings.BookingEvents;

namespace Bookings.Application.Queries;

public class MyBookingsProjection : MongoProjection<MyBookings> {
    public MyBookingsProjection(IMongoDatabase database) : base(database) {
        On<V1.RoomBooked>(b => b
            .UpdateOne
            .Id(ctx => ctx.Message.GuestId)
            .UpdateFromContext((ctx, update) =>
                update.AddToSet(
                    x => x.Bookings,
                    new MyBookings.Booking(ctx.Stream.GetId(),
                        ctx.Message.CheckInDate,
                        ctx.Message.CheckOutDate,
                        ctx.Message.BookingPrice
                    )
                )
            )
        );

        On<V1.BookingCancelled>(
            b => b.UpdateOne
                .Filter((ctx, doc) =>
                    doc.Bookings.Select(booking => booking.BookingId).Contains(ctx.Stream.GetId())
                )
                .UpdateFromContext((ctx, update) =>
                    update.PullFilter(
                        x => x.Bookings,
                        x => x.BookingId == ctx.Stream.GetId()
                    )
                )
        );
    }
}
