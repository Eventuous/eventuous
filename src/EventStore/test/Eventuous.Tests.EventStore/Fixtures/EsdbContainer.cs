using System.Runtime.InteropServices;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Fixtures;

public static class EsdbContainer {
    public static EventStoreDbContainer Create() {
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "eventstore/eventstore:23.10.0-alpha-arm64v8"
            : "eventstore/eventstore:23.10.0-bookworm-slim";

        return new EventStoreDbBuilder().WithImage(image).Build();
    }
}
