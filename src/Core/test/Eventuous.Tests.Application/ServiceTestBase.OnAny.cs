using Eventuous.Sut.App;
using Eventuous.Testing;

namespace Eventuous.Tests.Application;

public abstract partial class ServiceTestBase {
    [Test]
    public async Task Should_execute_on_any_no_stream() {
        var bookRoom = Helpers.GetBookRoom();

        var cmd = new Commands.ImportBooking {
            BookingId = "dummy",
            Price     = bookRoom.Price,
            CheckIn   = bookRoom.CheckIn,
            CheckOut  = bookRoom.CheckOut,
            RoomId    = bookRoom.RoomId
        };

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(cmd.BookingId)
            .When(cmd)
            .ThenAsync(async result => {
                    await result.ResultIsOkAsync(async x => await Assert.That(x.Changes).HasCount(1));
                    await result.StreamIsAsync(async x => await Assert.That(x).HasCount(1));
                }
            );
    }
}
