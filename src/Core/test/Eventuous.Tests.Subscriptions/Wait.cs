// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Polls for a condition the subscription reaches on its own schedule, so the timeout is the failure bound
/// rather than the test's running time.
/// </summary>
static class Wait {
    public static Task<bool> Until(Func<bool> condition, TimeSpan timeout) => Until(() => Task.FromResult(condition()), timeout);

    /// <summary>Checks once more after the deadline, so a boundary case isn't reported as a timeout.</summary>
    public static async Task<bool> Until(Func<Task<bool>> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            if (await condition()) return true;

            await Task.Delay(20);
        }

        return await condition();
    }
}
