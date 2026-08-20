// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Logging;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// A delay that can't be waited on has to fall back, loudly: left alone, a negative retry delay throws inside
/// the supervisor's <c>Task.Delay</c> and a negative teardown timeout never expires.
/// </summary>
public class SupervisorSettingsTests {
    // -1ms, which Task.Delay and CancellationTokenSource read as "never": the one negative that must survive.
    const long InfiniteMs = -1;

    // The largest finite wait both Task.Delay and CancellationTokenSource take, and the first one they refuse.
    const long MaxDelayMs     = uint.MaxValue - 1;
    const long OverMaxDelayMs = MaxDelayMs + 1;

    [Test]
    [Arguments(-5_000, 2_000, true)]
    [Arguments(InfiniteMs, InfiniteMs, false)]
    [Arguments(0, 0, false)]
    [Arguments(5_000, 5_000, false)]
    [Arguments(MaxDelayMs, MaxDelayMs, false)]
    [Arguments(OverMaxDelayMs, 2_000, true)]
    public void Retry_delay_is_replaced_only_when_it_cannot_be_waited_on(long configuredMs, long expectedMs, bool warns) {
        var logs = new CapturingLoggerFactory();

        var settings = SupervisorSettings.From(
            new TestOptions { SubscriptionId = "settings", RetryDelay = TimeSpan.FromMilliseconds(configuredMs) },
            Logger.CreateContext("settings", logs)
        );

        settings.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(expectedMs));
        settings.TeardownTimeout.ShouldBe(SubscriptionOptions.DefaultTeardownTimeout, "a valid teardown timeout must not be disturbed by the retry delay");

        logs.Contains("Retry delay").ShouldBe(warns, "a silent fallback leaves an operator reading a value the subscription isn't using");
        logs.Contains("Teardown timeout").ShouldBeFalse();
    }

    [Test]
    [Arguments(-5_000, 5_000, true)]
    [Arguments(InfiniteMs, InfiniteMs, false)]
    [Arguments(0, 0, false)]
    [Arguments(7_000, 7_000, false)]
    [Arguments(MaxDelayMs, MaxDelayMs, false)]
    [Arguments(OverMaxDelayMs, 5_000, true)]
    public void Teardown_timeout_is_replaced_only_when_it_cannot_be_waited_on(long configuredMs, long expectedMs, bool warns) {
        var logs = new CapturingLoggerFactory();

        var settings = SupervisorSettings.From(
            new TestOptions { SubscriptionId = "settings", TeardownTimeout = TimeSpan.FromMilliseconds(configuredMs) },
            Logger.CreateContext("settings", logs)
        );

        settings.TeardownTimeout.ShouldBe(TimeSpan.FromMilliseconds(expectedMs));
        settings.RetryDelay.ShouldBe(SubscriptionOptions.DefaultRetryDelay, "a valid retry delay must not be disturbed by the teardown timeout");

        logs.Contains("Teardown timeout").ShouldBe(warns);
        logs.Contains("Retry delay").ShouldBeFalse();
    }

    /// <summary>
    /// Both bad at once must both be reported, or fixing one means restarting to hear about the other.
    /// </summary>
    [Test]
    public void Two_unusable_settings_are_both_reported_and_both_fall_back() {
        var logs = new CapturingLoggerFactory();

        var settings = SupervisorSettings.From(
            new TestOptions {
                SubscriptionId  = "settings",
                RetryDelay      = TimeSpan.FromSeconds(-3),
                TeardownTimeout = TimeSpan.FromSeconds(-4)
            },
            Logger.CreateContext("settings", logs)
        );

        settings.RetryDelay.ShouldBe(SubscriptionOptions.DefaultRetryDelay);
        settings.TeardownTimeout.ShouldBe(SubscriptionOptions.DefaultTeardownTimeout);

        logs.Contains("Retry delay -00:00:03 cannot be waited on").ShouldBeTrue();
        logs.Contains("Teardown timeout -00:00:04 cannot be waited on").ShouldBeTrue();
    }

    /// <summary>
    /// The upper bound is the runtime's, not ours: this pins it on every target framework, so a setting the
    /// validator lets through can't still throw where the supervisor waits on it.
    /// </summary>
    [Test]
    public void Accepted_settings_are_settings_the_runtime_accepts() {
        var logs = new CapturingLoggerFactory();

        var settings = SupervisorSettings.From(
            new TestOptions {
                SubscriptionId  = "settings",
                RetryDelay      = TimeSpan.FromMilliseconds(MaxDelayMs),
                TeardownTimeout = TimeSpan.FromMilliseconds(MaxDelayMs)
            },
            Logger.CreateContext("settings", logs)
        );

        using var cts = new CancellationTokenSource();

        // Both are the calls the supervisor makes: the resubscribe delay and the graceful stop budget. The
        // delay tasks are discarded rather than awaited — what's under test is the argument validation both
        // do synchronously, not the wait itself.
        Should.NotThrow(() => { _ = Task.Delay(settings.RetryDelay, cts.Token); });
        Should.NotThrow(() => new CancellationTokenSource(settings.TeardownTimeout).Dispose());

        var overMax = TimeSpan.FromMilliseconds(OverMaxDelayMs);

        Should.Throw<ArgumentOutOfRangeException>(() => { _ = Task.Delay(overMax, cts.Token); });
        Should.Throw<ArgumentOutOfRangeException>(() => new CancellationTokenSource(overMax).Dispose());

        // Releases the 49-day timer the accepted delay armed, instead of leaving it for the rest of the run.
        cts.Cancel();

        logs.Contains("cannot be waited on").ShouldBeFalse("the runtime's own limit must not be reported as unusable");
    }

    record TestOptions : SubscriptionOptions;
}
