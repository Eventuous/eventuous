using System.Diagnostics;

namespace Eventuous.Diagnostics;

public static class DummyActivityListener {
    public static ActivityListener Create() => new() {
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        ShouldListenTo  = x => x.Name == EventuousDiagnostics.InstrumentationName,
        ActivityStarted = _ => { },
        ActivityStopped = _ => { }
    };
}