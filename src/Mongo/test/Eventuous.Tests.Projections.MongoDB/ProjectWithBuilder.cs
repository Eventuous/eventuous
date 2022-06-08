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
        var stream = StreamName.For<Booking>(evt.BookingId);

        var first = await Act(stream, evt, e => e.BookingId);

        var expected = new BookingDocument(evt.BookingId) {
            RoomId       = evt.RoomId,
            CheckInDate  = evt.CheckIn,
            CheckOutDate = evt.CheckOut,
            BookingPrice = evt.Price,
            Outstanding  = evt.Price,
            Position     = first.Append.GlobalPosition
        };

        first.Doc.Should().Be(expected);

        var payment = new BookingPaymentRegistered(evt.BookingId, Instance.Auto.Create<string>(), evt.Price);

        var second = await Act(stream, payment, x => x.BookingId);

        expected = expected with {
            PaidAmount = payment.AmountPaid,
            Position = second.Append.GlobalPosition
        };

        second.Doc.Should().Be(expected);
    }

    static async Task<(AppendEventsResult Append, BookingDocument? Doc)> Act<T>(
        StreamName      stream,
        T               evt,
        Func<T, string> getId
    )
        where T : class {
        var append = await Instance.AppendEvent(stream, evt);

        await Task.Delay(500);

        var actual = await Instance.Mongo.LoadDocument<BookingDocument>(getId(evt));
        return (append, actual);
    }

    public ProjectWithBuilder(ITestOutputHelper output) : base(nameof(ProjectWithBuilder), output) { }

    public class SutProjection : MongoProjection<BookingDocument> {
        public SutProjection(IMongoDatabase database) : base(database) {
            On<BookingImported>(
                b => b
                    .InsertOne
                    .Document(
                        evt => new BookingDocument(evt.BookingId) {
                            RoomId       = evt.RoomId,
                            CheckInDate  = evt.CheckIn,
                            CheckOutDate = evt.CheckOut,
                            BookingPrice = evt.Price,
                            Outstanding  = evt.Price
                        }
                    )
            );

            On<RoomBooked>(
                b => b
                    .InsertOne
                    .Document(
                        ctx => new BookingDocument(ctx.Message.BookingId) {
                            BookingPrice = ctx.Message.Price,
                            Outstanding  = ctx.Message.Price
                        }
                    )
            );

            On<BookingPaymentRegistered>(
                b => b
                    .UpdateOne
                    .Id(x => x.BookingId)
                    .Update(
                        (evt, update) => update.Set(x => x.PaidAmount, evt.AmountPaid)
                    )
            );
        }
    }
}
