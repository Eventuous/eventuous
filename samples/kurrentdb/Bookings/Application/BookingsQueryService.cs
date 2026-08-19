using Azure;
using Azure.Storage.Blobs;
using Bookings.Application.Queries;
using Eventuous.Azure.Storage.Blobs;
using Eventuous.Projections.MongoDB.Tools;
using MongoDB.Driver;

namespace Bookings.Application;

public class BookingsQueryService(IMongoDatabase database, BlobServiceClient blobClient, BlobStorageProjectorOptions blobOptions) {
    public async Task<MyBookings?> GetUserBookings(string userId) => await database.LoadDocument<MyBookings>(userId);

    /// <summary>
    /// Reads the booking state projected to Azure Blob Storage. The blob name follows the
    /// projector's default naming convention: {id}/{state type name}.json.
    /// </summary>
    public async Task<BookingView?> GetBooking(string bookingId, CancellationToken cancellationToken) {
        var blob = blobClient
            .GetBlobContainerClient(BookingStateBlobProjection.ContainerName)
            .GetBlobClient($"{bookingId}/{nameof(BookingView)}.json");

        try {
            var content = await blob.DownloadContentAsync(cancellationToken);

            return content.Value.Content.ToObjectFromJson<BookingView>(blobOptions.JsonOptions);
        } catch (RequestFailedException e) when (e.Status == 404) {
            return null;
        }
    }
}
