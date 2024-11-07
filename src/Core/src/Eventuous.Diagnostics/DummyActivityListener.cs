// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

public static class DummyActivityListener {
    public static ActivityListener Create()
        => new() {
            ShouldListenTo  = x => x.Name.StartsWith(EventuousDiagnostics.InstrumentationName), 
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
}
