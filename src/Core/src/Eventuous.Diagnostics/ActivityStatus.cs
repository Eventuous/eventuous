// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

public record ActivityStatus(ActivityStatusCode StatusCode, string? Description, Exception? Exception) {
    static readonly ActivityStatus PlainOk = new(ActivityStatusCode.Ok, null, null);

    // Shared when there's no description: the record is immutable, and this is the status of every successful span.
    public static ActivityStatus Ok(string? description = null)
        => description == null ? PlainOk : new(ActivityStatusCode.Ok, description, null);

    public static ActivityStatus Error(Exception? exception = null, string? description = null)
        => new(ActivityStatusCode.Error, description ?? exception?.Message, exception);
}
