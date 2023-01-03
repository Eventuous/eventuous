using Eventuous.Sut.App;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes();

    public static Commands.ImportBooking CreateImportBooking() {
        var from = Instance.Auto.Create<LocalDate>();

        return Instance.Auto.Build<Commands.ImportBooking>()
            .With(x => x.CheckIn, from)
            .With(x => x.CheckOut, from.PlusDays(2))
            .Create();
    }
}
