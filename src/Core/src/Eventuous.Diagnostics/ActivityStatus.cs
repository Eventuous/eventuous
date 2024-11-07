// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

public record ActivityStatus(ActivityStatusCode StatusCode, string? Description, Exception? Exception) {
    public static ActivityStatus Ok(string? description = null)
        => new(ActivityStatusCode.Ok, description, null);

    public static ActivityStatus Error(Exception? exception = null, string? description = null)
        => new(ActivityStatusCode.Error, description ?? exception?.Message, exception);
}
