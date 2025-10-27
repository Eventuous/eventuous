// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.TestHelpers.TUnit;

public static class IntToTimespan {
    public static TimeSpan Seconds(this int value) => TimeSpan.FromSeconds(value);

    public static TimeSpan Milliseconds(this int value) => TimeSpan.FromMilliseconds(value);
}
