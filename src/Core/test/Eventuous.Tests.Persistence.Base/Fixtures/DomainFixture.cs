using AutoFixture;
using Eventuous.Sut.App;
using NodaTime;

namespace Eventuous.Tests.Persistence.Base.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes(typeof(DomainFixture).Assembly);

    public static Commands.ImportBooking CreateImportBooking(IFixture auto) {
        var from = auto.Create<LocalDate>();

        return auto.Build<Commands.ImportBooking>()
            .With(x => x.CheckIn, from)
            .With(x => x.CheckOut, from.PlusDays(2))
            .Create();
    }
}
