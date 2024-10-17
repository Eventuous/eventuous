using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using MicroElements.AutoFixture.NodaTime;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);

    static IFixture Auto { get; } = new Fixture().Customize(new NodaTimeCustomization());

    public static Commands.ImportBooking CreateImportBooking() {
        var from = Auto.Create<LocalDate>();

        return Auto.Build<Commands.ImportBooking>()
            .With(x => x.CheckIn, from)
            .With(x => x.CheckOut, from.PlusDays(2))
            .Create();
    }
}
