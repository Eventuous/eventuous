// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.TestHelpers.TUnit;

public static class IntToTimespan {
    extension(int value) {
        public TimeSpan Seconds() => TimeSpan.FromSeconds(value);
        public TimeSpan Milliseconds() => TimeSpan.FromMilliseconds(value);
    }
}
