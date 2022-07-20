using Eventuous.Projections.MongoDB;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using MongoDB.Driver;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Projections.MongoDB.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Projections.MongoDB;

public sealed class ProjectingWithTypedHandlers : ProjectionTestBase<ProjectingWithTypedHandlers.SutProjection> {
    [Fact]
    public async Task ShouldProjectImported() {
        var evt    = DomainFixture.CreateImportBooking();
        var id     = new BookingId(CreateId());
        var stream = StreamName.For<Booking, BookingState, BookingId>(id);

        var append = await Instance.AppendEvent(stream, evt);

        await Task.Delay(500);

        var expected = new BookingDocument(id.ToString()) {
            RoomId       = evt.RoomId,
            CheckInDate  = evt.CheckIn,
            CheckOutDate = evt.CheckOut,
            BookingPrice = evt.Price,
            Position     = append.GlobalPosition
        };

        var actual = await Instance.Mongo.LoadDocument<BookingDocument>(id.ToString());
        actual.Should().Be(expected);
    }

    public ProjectingWithTypedHandlers(ITestOutputHelper output) : base(nameof(ProjectingWithTypedHandlers), output) { }

    public class SutProjection : MongoProjection<BookingDocument> {
        public SutProjection(IMongoDatabase database) : base(database) {
            On<BookingImported>(
                stream => stream.GetId(),
                (ctx, update) => update
                    .SetOnInsert(x => x.RoomId, ctx.Message.RoomId)
                    .Set(x => x.CheckInDate, ctx.Message.CheckIn)
                    .Set(x => x.CheckOutDate, ctx.Message.CheckOut)
            );
        }
    }
}
