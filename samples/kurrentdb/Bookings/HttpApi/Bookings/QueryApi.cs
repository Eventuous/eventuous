using Bookings.Domain.Bookings;
using Eventuous;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.HttpApi.Bookings;

[Route("/bookings")]
public class QueryApi(IEventStore store) : ControllerBase {
    readonly StreamNameMap _streamNameMap = new();

    [HttpGet]
    [Route("{id}")]
    public async Task<BookingState> GetBooking(string id, CancellationToken cancellationToken) {
        var booking = await store.LoadState<BookingState, BookingId>(_streamNameMap, new(id), cancellationToken: cancellationToken);

        return booking.State;
    }
}
