using System.Runtime.InteropServices;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class EsdbContainer {
    public static EventStoreDbContainer Create() {
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "eventstore/eventstore:24.6.0-alpha-arm64v8"
            : "eventstore/eventstore:24.6";

        return new EventStoreDbBuilder()
            .WithImage(image)
            .WithEnvironment("EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP", "true")
            // .WithCleanUp(false)
            // .WithAutoRemove(false)
            .Build();
    }
}
