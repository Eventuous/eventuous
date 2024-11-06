using Eventuous.Sut.App;
using Eventuous.Testing;
using Shouldly;

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
            .Then(result => result.ResultIsOk(x => x.Changes.Should().HaveCount(1)).StreamIs(x => x.Length.ShouldBe(1)));
    }
}
