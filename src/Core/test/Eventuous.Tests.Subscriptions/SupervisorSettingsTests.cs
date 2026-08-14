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

    [Test]
    [Arguments(-5_000, 2_000, true)]
    [Arguments(InfiniteMs, InfiniteMs, false)]
    [Arguments(0, 0, false)]
    [Arguments(5_000, 5_000, false)]
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

    record TestOptions : SubscriptionOptions;
}
