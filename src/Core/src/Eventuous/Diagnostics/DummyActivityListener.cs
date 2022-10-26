using System.Diagnostics;

namespace Eventuous.Diagnostics;

public static class DummyActivityListener {
    public static ActivityListener Create()
        => new() { ShouldListenTo  = x => x.Name == EventuousDiagnostics.InstrumentationName };
}
