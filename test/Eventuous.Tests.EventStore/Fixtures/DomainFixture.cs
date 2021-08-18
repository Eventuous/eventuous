using System;
using AutoFixture;
using Eventuous.Tests.SutApp;
using NodaTime;

namespace Eventuous.Tests.EventStore.Fixtures {
    public static class DomainFixture {
        static readonly Fixture Fixture = new();

        public static Commands.ImportBooking CreateImportBooking() {
            var from = Fixture.Create<DateTime>();

            return new Commands.ImportBooking(
                Fixture.Create<string>(),
                Fixture.Create<string>(),
                LocalDate.FromDateTime(from),
                LocalDate.FromDateTime(from.AddDays(Fixture.Create<int>()))
            );
        }
    }
}