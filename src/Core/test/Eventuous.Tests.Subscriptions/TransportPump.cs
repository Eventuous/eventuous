// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// The contract every transport's reading loop keeps: report its death as this run's failure unless the run's
/// own token ended it.
/// </summary>
/// <remarks>
/// Test infrastructure, deliberately. The supervisor has no pump classification of its own — each transport
/// decides (see <c>SqlSubscriptionBase.Connect</c>) — so keeping the fakes' copy here stops a test asserting
/// on it and calling that production behaviour.
/// </remarks>
static class TransportPump {
    public static async Task Run(SubscriptionRun run, Func<Task> loop, string endedWhileHealthy) {
        try {
            await loop().ConfigureAwait(false);

            if (!run.Token.IsCancellationRequested) run.Fail(DropReason.ServerError, new InvalidOperationException(endedWhileHealthy));
        } catch (OperationCanceledException e) when (!run.Token.IsCancellationRequested) {
            run.Fail(DropReason.ServerError, e);
        } catch (OperationCanceledException) {
            // The run's own token asked for this: graceful, not a drop.
        } catch (Exception e) {
            run.Fail(DropReason.ServerError, e);
        }
    }
}
