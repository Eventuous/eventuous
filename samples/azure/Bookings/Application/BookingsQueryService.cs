using Bookings.Application.Queries;

namespace Bookings.Application;

public class BookingsQueryService(MyBookingsProjection projection) {
    public async Task<MyBookings?> GetUserBookings(string userId) => await projection.LoadDocument(userId);
}
