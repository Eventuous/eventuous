using Eventuous.Sut.App;
using Eventuous.Testing;

namespace Eventuous.Tests.Application;

public class ThrowingCommandServiceTests {
    readonly ThrowingCommandService<Sut.Domain.BookingState> _service;

    public ThrowingCommandServiceTests() => _service = new(new BookingService(new InMemoryEventStore()));

    [Test]
    public async Task ShouldReturnResultOnSuccess(CancellationToken cancellationToken) {
        var result = await _service.Handle(Helpers.GetBookRoom(), cancellationToken);

        await Assert.That(result.TryGet(out _)).IsTrue();
    }

    [Test]
    public async Task ShouldThrowOnError(CancellationToken cancellationToken) {
        var cmd = Helpers.GetBookRoom();
        await _service.Handle(cmd, cancellationToken);

        // The booking already exists, so the second attempt produces an error result
        await Assert.That(async () => await _service.Handle(cmd, cancellationToken)).Throws<WrongVersion>();
    }
}
