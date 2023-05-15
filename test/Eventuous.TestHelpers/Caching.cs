// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Eventuous.TestHelpers {
    public static class Caching {
        public static IMemoryCache CreateMemoryCache() => new MemoryCache(Options.Create<MemoryCacheOptions>(new()));
    }
}
