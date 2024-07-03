// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Tests.Fixtures;

public class IdGenerator {
    public static string GetId() => Guid.NewGuid().ToString("N");
}
