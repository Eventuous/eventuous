using Eventuous.Projections.MongoDB;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using MongoDB.Driver;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Projections.MongoDB;

[ClassDataSource<IntegrationFixture>]
public class ProjectWithBuilder(IntegrationFixture fixture) : ProjectionTestBase<ProjectWithBuilder.SutProjection>(nameof(ProjectWithBuilder), fixture) {
    [Test]
    public async Task ShouldProjectImported() {
        var evt    = DomainFixture.CreateImportBooking();
        var id     = new BookingId(CreateId());
        var stream = StreamNameFactory.For<Booking, BookingState, BookingId>(id);

        var first = await Act(stream, evt);

        var expected = new BookingDocument(id.ToString()) {
            RoomId         = evt.RoomId,
            CheckInDate    = evt.CheckIn,
            CheckOutDate   = evt.CheckOut,
            BookingPrice   = evt.Price,
            Outstanding    = evt.Price,
            Position       = first.Append.GlobalPosition,
            StreamPosition = (ulong)first.Append.NextExpectedVersion
        };

        first.Doc.Should().BeEquivalentTo(expected);

        var payment = new BookingPaymentRegistered(Fixture.Auto.Create<string>(), evt.Price);

        var second = await Act(stream, payment);

        expected = expected with {
            PaidAmount = payment.AmountPaid,
            Position = second.Append.GlobalPosition,
            StreamPosition = (ulong)second.Append.NextExpectedVersion
        };

        second.Doc.Should().BeEquivalentTo(expected);
    }

    async Task<(AppendEventsResult Append, BookingDocument? Doc)> Act<T>(StreamName stream, T evt)
        where T : class {
        var append = await Fixture.AppendEvent(stream, evt);
        await WaitForPosition(append.GlobalPosition);
        var actual = await Fixture.Mongo.LoadDocument<BookingDocument>(stream.GetId());

        return (append, actual);
    }

    public class SutProjection : MongoProjector<BookingDocument> {
        public SutProjection(IMongoDatabase database)
            : base(database) {
            On<BookingImported>(
                b => b
                    .InsertOne
                    .Document(
                        (stream, e) => new(stream.GetId()) {
                            RoomId       = e.RoomId,
                            CheckInDate  = e.CheckIn,
                            CheckOutDate = e.CheckOut,
                            BookingPrice = e.Price,
                            Outstanding  = e.Price
                        }
                    )
            );

            On<RoomBooked>(
                b => b
                    .InsertOne
                    .Document(
                        ctx => new(ctx.Stream.GetId()) {
                            BookingPrice = ctx.Message.Price,
                            Outstanding  = ctx.Message.Price
                        }
                    )
            );

            On<BookingPaymentRegistered>(
                b => b
                    .UpdateOne
                    .DefaultId()
                    .Update((evt, update) => update.Set(x => x.PaidAmount, evt.AmountPaid))
            );
        }
    }
}
