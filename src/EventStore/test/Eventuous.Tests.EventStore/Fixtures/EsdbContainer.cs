using System.Runtime.InteropServices;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class EsdbContainer {
    public static EventStoreDbContainer Create() {
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "eventstore/eventstore:22.10.5-alpha-arm64v8"
            : "eventstore/eventstore:22.10.5-bookworm-slim";

        return new EventStoreDbBuilder()
            .WithImage(image)
            .WithEnvironment("EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP", "true")
            // .WithCleanUp(false)
            // .WithAutoRemove(false)
            .Build();
    }
}
