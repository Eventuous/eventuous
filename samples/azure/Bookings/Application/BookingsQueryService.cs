using Bookings.Application.Queries;

namespace Bookings.Application;

public class BookingsQueryService([FromKeyedServices("BookingsProjections")] MyBookingsProjection projection) {
    public async Task<MyBookings?> GetUserBookings(string userId) => await projection.LoadDocument(userId);
}
