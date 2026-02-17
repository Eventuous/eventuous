using System.Runtime.InteropServices;
using Testcontainers.KurrentDb;

namespace Eventuous.Tests.KurrentDB.Fixtures;

public static class KurrentDBContainer {
    public static KurrentDbContainer Create() {
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "kurrentplatform/kurrentdb:25.1.3-experimental-arm64-8.0-jammy"
            : "kurrentplatform/kurrentdb:25.1.3";

        return new KurrentDbBuilder()
            .WithImage(image)
            .WithEnvironment("KURRENTDB_ENABLE_ATOM_PUB_OVER_HTTP", "true")
            .Build();
    }
}
