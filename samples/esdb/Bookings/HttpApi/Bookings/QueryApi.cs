using Bookings.Domain.Bookings;
using Eventuous;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.HttpApi.Bookings;

[Route("/bookings")]
public class QueryApi : ControllerBase {
    readonly IAggregateStore _store;
        
    public QueryApi(IAggregateStore store) => _store = store;

    [HttpGet]
    [Route("{id}")]
    public async Task<BookingState> GetBooking(string id, CancellationToken cancellationToken) {
        var booking = await _store.Load<Booking>(StreamName.For<Booking>(id), cancellationToken);
        return booking.State;
    }
}