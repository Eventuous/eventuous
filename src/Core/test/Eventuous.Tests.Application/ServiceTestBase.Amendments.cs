using Eventuous.Sut.Domain;
using Eventuous.Testing;

namespace Eventuous.Tests.Application;

public abstract partial class ServiceTestBase {
    [Fact]
    public async Task Should_amend_event_from_command() {
        var service = CreateService(amendEvent: AmendEvent);
        var cmd     = CreateCommand();

        await service.Handle(cmd, default);

        var stream = await Store.ReadStream(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start);
        stream[0].Metadata["userId"].Should().Be(cmd.ImportedBy);
    }

    [Fact]
    public async Task Should_amend_event_with_static_meta() {
        var cmd = Helpers.GetBookRoom();

        await CommandServiceFixture
            .ForService(() => CreateService(amendAll: AddMeta), Store)
            .Given(cmd.BookingId)
            .When(cmd)
            .Then(x => x.StreamIs(e => e[0].Metadata["foo"].Should().Be("bar")));
    }

    [Fact]
    public async Task Should_combine_amendments() {
        var service = CreateService(amendEvent: AmendEvent, amendAll: AddMeta);
        var cmd     = CreateCommand();

        await service.Handle(cmd, default);

        var stream = await Store.ReadStream(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start);
        stream[0].Metadata["userId"].Should().Be(cmd.ImportedBy);
        stream[0].Metadata["foo"].Should().Be("bar");
    }

    static NewStreamEvent AmendEvent(NewStreamEvent evt, ImportBooking cmd) => evt with { Metadata = evt.Metadata.With("userId", cmd.ImportedBy) };

    static NewStreamEvent AddMeta(NewStreamEvent evt) => evt with { Metadata = evt.Metadata.With("foo", "bar") };
}
