using Eventuous.Sut.Domain;

namespace Eventuous.Tests.Application;

public class ResolverNullCheckTests {
    [Test]
    public async Task ShouldFailAtCallTimeWhenNoReaderIsAvailable(CancellationToken cancellationToken) {
        var service = new NoStoreService();

        var exception = await Assert.That(async () => await service.Handle(Helpers.GetBookRoom(), cancellationToken)).Throws<ArgumentNullException>();

        await Assert.That(exception!.Message).Contains("Function to resolve event reader from BookRoom is not defined and no default reader is set");
    }

    class NoStoreService : CommandService<Booking, BookingState, BookingId> {
        public NoStoreService() : base((IEventStore?)null) {
            On<Sut.App.Commands.BookRoom>()
                .InState(ExpectedState.New)
                .GetId(cmd => new(cmd.BookingId))
                .Act((_, _) => { });
        }
    }
}
