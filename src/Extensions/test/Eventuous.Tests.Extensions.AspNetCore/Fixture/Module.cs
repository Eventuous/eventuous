using System.Runtime.CompilerServices;
using VerifyTests.DiffPlex;

namespace Eventuous.Tests.Extensions.AspNetCore.Fixture;

public static class ModuleInitializer {
    [ModuleInitializer]
    public static void Initialize() {
        TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);
        VerifyDiffPlex.Initialize(OutputType.Compact);
    }
}
