namespace Eventuous.Tests.Aggregates;

using Sut.Domain;

public class ChangesViewTests {
    [Test]
    public async Task ShouldReuseTheSameViewThatReflectsNewChanges() {
        var booking = new Booking();
        var view    = booking.Changes;

        await Assert.That(view).HasCount(0);
        await Assert.That(booking.CurrentVersion).IsEqualTo(-1);

        booking.Cancel();

        await Assert.That(booking.Changes).IsSameReferenceAs(view);
        await Assert.That(view).HasCount(1);
        await Assert.That(booking.CurrentVersion).IsEqualTo(0);

        booking.ClearChanges();

        await Assert.That(booking.Changes).IsSameReferenceAs(view);
        await Assert.That(view).HasCount(0);
        await Assert.That(booking.CurrentVersion).IsEqualTo(-1);
    }
}
