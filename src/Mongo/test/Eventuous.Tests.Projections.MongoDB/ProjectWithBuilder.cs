using Eventuous.Projections.MongoDB;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using MongoDB.Driver;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Projections.MongoDB;

[ClassDataSource<IntegrationFixture>]
public class ProjectWithBuilder(IntegrationFixture fixture) {
    [Test]
    [MethodDataSource(typeof(CollectionSource), nameof(CollectionSource.TestOptions))]
    public async Task ShouldProjectImported(MongoProjectionOptions<BookingDocument>? options) {
        var projectionFixture = new ProjectionTestBase<SutProjection>(nameof(ProjectWithBuilder), fixture);
        var evt               = DomainFixture.CreateImportBooking();
        var id                = new BookingId(projectionFixture.CreateId());
        var stream            = StreamNameFactory.For<Booking, BookingState, BookingId>(id);
        
        await projectionFixture.InitializeAsync();

        var first = await Act(projectionFixture, stream, evt);

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

        var payment = new BookingPaymentRegistered(projectionFixture.Fixture.Auto.Create<string>(), evt.Price);

        var second = await Act(projectionFixture, stream, payment);

        await projectionFixture.DisposeAsync();

        expected = expected with {
            PaidAmount = payment.AmountPaid,
            Position = second.Append.GlobalPosition,
            StreamPosition = (ulong)second.Append.NextExpectedVersion
        };

        second.Doc.Should().BeEquivalentTo(expected);
    }

    static async Task<(AppendEventsResult Append, BookingDocument? Doc)> Act<T>(ProjectionTestBase<SutProjection> f, StreamName stream, T evt) where T : class {
        var append = await f.Fixture.AppendEvent(stream, evt);
        await f.WaitForPosition(append.GlobalPosition);
        var actual = await f.Fixture.Mongo.LoadDocument<BookingDocument>(stream.GetId());

        return (append, actual);
    }

    public class SutProjection : MongoProjector<BookingDocument> {
        public SutProjection(IMongoDatabase database) : base(database) {
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

public static class CollectionSource {
    public static IEnumerable<MongoProjectionOptions<BookingDocument>?> TestOptions() {
        yield return null;
        yield return new() { CollectionName = "test" };
    }
}
