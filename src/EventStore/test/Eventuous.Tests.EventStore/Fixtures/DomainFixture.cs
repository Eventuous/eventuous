using Eventuous.Sut.App;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class DomainFixture {
    static DomainFixture() => TypeMap.RegisterKnownEventTypes();
        
    public static Commands.ImportBooking CreateImportBooking() {
        var from = Instance.Auto.Create<DateTime>();

        return new Commands.ImportBooking(
            Instance.Auto.Create<string>(),
            Instance.Auto.Create<string>(),
            LocalDate.FromDateTime(from),
            LocalDate.FromDateTime(from.AddDays(Instance.Auto.Create<int>())),
            Instance.Auto.Create<decimal>()
        );
    }
}