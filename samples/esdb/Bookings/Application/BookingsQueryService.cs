using Bookings.Application.Queries;
using Eventuous.Projections.MongoDB.Tools;
using MongoDB.Driver;

namespace Bookings.Application;

public class BookingsQueryService(IMongoDatabase database) {
    public async Task<MyBookings?> GetUserBookings(string userId) => await database.LoadDocument<MyBookings>(userId);
}
