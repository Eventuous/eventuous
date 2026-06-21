using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Bookings.Domain.Bookings;
using Eventuous;
using Eventuous.Azure.Storage.Blobs;
using Eventuous.Subscriptions.Context;
using static Bookings.Domain.Bookings.BookingEvents;

namespace Bookings.Application.Queries;

public class MyBookingsProjection : StorageBlobsProjector<MyBookings> {
    readonly IEventReader eventReader;

    public MyBookingsProjection(BlobServiceClient client, IEventReader eventReader) : base(client, "bookings-container") {
        this.eventReader = eventReader;
        
        On<V1.RoomBooked>(AddBooking, ctx => new ValueTask<string>(ctx.Message.GuestId));
        On<V1.BookingCancelled>(CancelBooking, GetGuestIdFromStateAsync);
    }

    private async ValueTask<string> GetGuestIdFromStateAsync(IMessageConsumeContext<V1.BookingCancelled> ctx) {
        var folded = await eventReader.LoadState<BookingState>(ctx.Stream, true, ctx.CancellationToken);
        return folded.State.GuestId ?? throw new InvalidOperationException("MyBookings not found");
    }

    private static MyBookings AddBooking(IMessageConsumeContext<V1.RoomBooked> ctx, MyBookings b) => b with {
        Bookings = b.Bookings.Add(new(ctx.Stream.GetId(), ctx.Message.CheckInDate, ctx.Message.CheckOutDate, ctx.Message.BookingPrice))
    };

    private static MyBookings CancelBooking(IMessageConsumeContext<V1.BookingCancelled> ctx, MyBookings b) => b with {
        Bookings = b.Bookings.RemoveAll(booking => booking.BookingId == ctx.Stream.GetId())
    };

    public async Task<MyBookings?> LoadDocument(string userId) {
        try {
            var blobName = GetBlobName(userId);
            var blobClient = ContainerClient.GetBlobClient(blobName);
            BlobDownloadResult blobContent = await blobClient.DownloadContentAsync();
            return Deserialize(blobContent.Content);
        } catch (RequestFailedException ex) when (ex.Status == 404) {
            return null;
        }
    }
}
