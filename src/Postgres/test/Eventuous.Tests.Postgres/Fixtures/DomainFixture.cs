using Eventuous.Sut.App;
using NodaTime;
using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Postgres.Fixtures;

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