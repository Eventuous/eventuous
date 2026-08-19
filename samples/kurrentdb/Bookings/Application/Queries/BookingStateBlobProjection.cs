using Azure.Storage.Blobs;
using Eventuous.Azure.Storage.Blobs;
using static Bookings.Domain.Bookings.BookingEvents;

namespace Bookings.Application.Queries;

/// <summary>
/// Projects the booking state to Azure Blob Storage, in parallel with the MongoDB projections
/// registered on the same subscription. Each booking stream becomes one JSON blob.
/// The all-stream subscription provides real global positions, so the projector can use
/// ByGlobalPosition idempotency to skip replayed events.
/// </summary>
public class BookingStateBlobProjection : BlobStorageProjector<BookingView> {
    public const string ContainerName = "bookings";

    public BookingStateBlobProjection(BlobServiceClient client, BlobStorageProjectorOptions options)
        : base(client, ContainerName, options) {
        On<V1.RoomBooked>((ctx, view) => view with {
                Id = ctx.Stream.GetId(),
                GuestId = ctx.Message.GuestId,
                RoomId = ctx.Message.RoomId,
                CheckInDate = ctx.Message.CheckInDate,
                CheckOutDate = ctx.Message.CheckOutDate,
                BookingPrice = ctx.Message.BookingPrice,
                Outstanding = ctx.Message.OutstandingAmount
            }
        );

        On<V1.PaymentRecorded>((view, evt) => view with { Outstanding = evt.Outstanding });

        On<V1.BookingFullyPaid>((view, _) => view with { Paid = true });
    }
}
