using Eventuous.Projections.MongoDB;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using MongoDB.Driver;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Projections.MongoDB.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Projections.MongoDB;

public class ProjectWithBuilder : ProjectionTestBase<ProjectWithBuilder.SutProjection> {
    [Fact]
    public async Task ShouldProjectImported() {
        var evt    = DomainFixture.CreateImportBooking();
        var id     = new BookingId(CreateId());
        var stream = StreamName.For<Booking, BookingState, BookingId>(id);

        var first = await Act(stream, evt);

        var expected = new BookingDocument(id.ToString()) {
            RoomId       = evt.RoomId,
            CheckInDate  = evt.CheckIn,
            CheckOutDate = evt.CheckOut,
            BookingPrice = evt.Price,
            Outstanding  = evt.Price,
            Position     = first.Append.GlobalPosition
        };

        first.Doc.Should().Be(expected);

        var payment = new BookingPaymentRegistered(Instance.Auto.Create<string>(), evt.Price);

        var second = await Act(stream, payment);

        expected = expected with {
            PaidAmount = payment.AmountPaid,
            Position = second.Append.GlobalPosition
        };

        second.Doc.Should().Be(expected);
    }

    static async Task<(AppendEventsResult Append, BookingDocument? Doc)> Act<T>(StreamName stream, T evt)
        where T : class {
        var append = await Instance.AppendEvent(stream, evt);

        await Task.Delay(500);

        var actual = await Instance.Mongo.LoadDocument<BookingDocument>(stream.GetId());
        return (append, actual);
    }

    public ProjectWithBuilder(ITestOutputHelper output) : base(nameof(ProjectWithBuilder), output) { }

    public class SutProjection : MongoProjection<BookingDocument> {
        public SutProjection(IMongoDatabase database) : base(database) {
            On<BookingImported>(
                b => b
                    .InsertOne
                    .Document(
                        (stream, e) => new BookingDocument(stream.GetId()) {
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
                        ctx => new BookingDocument(ctx.Stream.GetId()) {
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
