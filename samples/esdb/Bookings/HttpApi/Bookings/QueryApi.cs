using Bookings.Domain.Bookings;
using Eventuous;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.HttpApi.Bookings;

[Route("/bookings")]
public class QueryApi(IAggregateStore store) : ControllerBase {
    [HttpGet]
    [Route("{id}")]
    public async Task<BookingState> GetBooking(string id, CancellationToken cancellationToken) {
        var booking = await store.Load<Booking, BookingState>(StreamName.For<Booking>(id), cancellationToken);
        return booking.State;
    }
}