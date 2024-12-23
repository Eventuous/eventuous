using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Testing;

namespace Eventuous.Tests.Application;

public abstract partial class ServiceTestBase {
    [Test]
    public async Task Should_run_on_new_no_stream() {
        var cmd      = Helpers.GetBookRoom();
        var expected = new BookingEvents.RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price);

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(cmd.BookingId)
            .When(cmd)
            .Then(result => result.ResultIsOk(x => x.Changes.Should().HaveCount(1)).FullStreamEventsAre(expected));
    }
    
    [Test]
    public async Task Should_fail_on_new_stream_exists() {
        var cmd      = Helpers.GetBookRoom();
        var seed = new BookingEvents.RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price);

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(cmd.BookingId, seed)
            .When(cmd)
            .Then(result => result.ResultIsError<WrongVersion>());
    }

    [Test]
    public async Task Should_execute_amended_append() {
        var seedCmd = Helpers.GetBookRoom();
        var seed    = new BookingEvents.RoomBooked(seedCmd.RoomId, seedCmd.CheckIn, seedCmd.CheckOut, seedCmd.Price);
        var cmd     = new Commands.ExecuteNoMatterWhat(seedCmd.BookingId);

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(seedCmd.BookingId, seed)
            .When(cmd)
            .Then(result => result.ResultIsOk(x => x.Changes.Should().HaveCount(1)).FullStreamEventsAre(seed, new BookingEvents.Executed()));
    }
}
