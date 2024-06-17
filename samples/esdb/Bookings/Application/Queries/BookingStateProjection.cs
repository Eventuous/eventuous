using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions.Context;
using MongoDB.Driver;
using static Bookings.Domain.Bookings.BookingEvents;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Bookings.Application.Queries;

public class BookingStateProjection : MongoProjection<BookingDocument> {
    public BookingStateProjection(IMongoDatabase database) : base(database) {
        On<V1.RoomBooked>(stream => stream.GetId(), HandleRoomBooked);

        On<V1.PaymentRecorded>(
            b => b
                .UpdateOne
                .DefaultId()
                .Update((evt, update) =>
                    update.Set(x => x.Outstanding, evt.Outstanding)
                )
        );

        On<V1.BookingFullyPaid>(b => b
            .UpdateOne
            .DefaultId()
            .Update((_, update) => update.Set(x => x.Paid, true))
        );
    }

    static UpdateDefinition<BookingDocument> HandleRoomBooked(
        IMessageConsumeContext<V1.RoomBooked> ctx, UpdateDefinitionBuilder<BookingDocument> update
    ) {
        var evt = ctx.Message;

        return update.SetOnInsert(x => x.Id, ctx.Stream.GetId())
            .Set(x => x.GuestId, evt.GuestId)
            .Set(x => x.RoomId, evt.RoomId)
            .Set(x => x.CheckInDate, evt.CheckInDate)
            .Set(x => x.CheckOutDate, evt.CheckOutDate)
            .Set(x => x.BookingPrice, evt.BookingPrice)
            .Set(x => x.Outstanding, evt.OutstandingAmount);
    }
}
