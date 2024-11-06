using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Testing;
using Shouldly;

namespace Eventuous.Tests.Application;

public abstract partial class ServiceTestBase {
    [Test]
    public async Task Should_execute_on_existing_stream_exists() {
        var seedCmd = Helpers.GetBookRoom();
        var seed    = new BookingEvents.RoomBooked(seedCmd.RoomId, seedCmd.CheckIn, seedCmd.CheckOut, seedCmd.Price);

        var paymentTime = DateTimeOffset.Now;
        var cmd         = new Commands.RecordPayment(new(seedCmd.BookingId), "444", new(seedCmd.Price), paymentTime);

        var expectedResult = new object[] {
            new BookingEvents.BookingPaymentRegistered(cmd.PaymentId, cmd.Amount.Amount),
            new BookingEvents.BookingOutstandingAmountChanged(0),
            new BookingEvents.BookingFullyPaid(paymentTime)
        };

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(seedCmd.BookingId, seed)
            .When(cmd)
            .Then(result => result.ResultIsOk().NewStreamEventsAre(expectedResult));
    }

    [Test]
    public async Task Should_fail_on_existing_no_stream() {
        var seedCmd     = Helpers.GetBookRoom();
        var paymentTime = DateTimeOffset.Now;
        var cmd         = new Commands.RecordPayment(new(seedCmd.BookingId), "444", new(seedCmd.Price), paymentTime);

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(seedCmd.BookingId)
            .When(cmd)
            .Then(result => result.ResultIsError().StreamIs(x => x.Length.ShouldBe(0)));
    }
}
