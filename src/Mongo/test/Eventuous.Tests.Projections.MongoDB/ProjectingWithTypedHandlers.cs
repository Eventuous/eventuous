using Eventuous.Projections.MongoDB;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using MongoDB.Driver;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Projections.MongoDB;

public sealed class ProjectingWithTypedHandlers(IntegrationFixture fixture, ITestOutputHelper output)
    : ProjectionTestBase<ProjectingWithTypedHandlers.SutProjection>(nameof(ProjectingWithTypedHandlers), fixture, output) {
    [Fact]
    public async Task ShouldProjectImported() {
        var evt    = DomainFixture.CreateImportBooking();
        var id     = new BookingId(CreateId());
        var stream = StreamNameFactory.For<Booking, BookingState, BookingId>(id);

        var append = await Fixture.AppendEvent(stream, evt);

        await Task.Delay(500);

        var expected = new BookingDocument(id.ToString()) {
            RoomId         = evt.RoomId,
            CheckInDate    = evt.CheckIn,
            CheckOutDate   = evt.CheckOut,
            BookingPrice   = evt.Price,
            Outstanding    = evt.Price,
            Position       = append.GlobalPosition,
            StreamPosition = (ulong)append.NextExpectedVersion
        };

        var actual = await Fixture.Mongo.LoadDocument<BookingDocument>(id.ToString());
        actual.Should().Be(expected);
    }

    public class SutProjection : MongoProjector<BookingDocument> {
        public SutProjection(IMongoDatabase database)
            : base(database) {
            On<BookingImported>(
                stream => stream.GetId(),
                (ctx, update) => update
                    .SetOnInsert(x => x.RoomId, ctx.Message.RoomId)
                    .Set(x => x.CheckInDate, ctx.Message.CheckIn)
                    .Set(x => x.CheckOutDate, ctx.Message.CheckOut)
                    .Set(x => x.BookingPrice, ctx.Message.Price)
                    .Set(x => x.Outstanding, ctx.Message.Price)
            );
        }
    }
}
