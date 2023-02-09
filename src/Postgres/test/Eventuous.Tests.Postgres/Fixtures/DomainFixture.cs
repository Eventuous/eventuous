using Eventuous.Sut.App;
using MicroElements.AutoFixture.NodaTime;
using NodaTime;
using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Postgres.Fixtures;

public static class DomainFixture {
    static IFixture Auto { get; } = new Fixture().Customize(new NodaTimeCustomization());

    static DomainFixture() => TypeMap.RegisterKnownEventTypes();
        
    public static Commands.ImportBooking CreateImportBooking() {
        var from = Auto.Create<LocalDate>();

        return Auto.Build<Commands.ImportBooking>()
            .With(x => x.CheckIn, from)
            .With(x => x.CheckOut, from.PlusDays(2))
            .Create();
    }
}