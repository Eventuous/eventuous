// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Tools;

namespace Eventuous.KurrentDB.Subscriptions.Diagnostics;

abstract class BaseSubscriptionMeasure(string subscriptionId, string streamName, KurrentDBClient client) {
    protected readonly KurrentDBClient Client = client;

    protected abstract IAsyncEnumerable<ResolvedEvent> Read(CancellationToken cancellationToken);

    protected abstract ulong GetLastPosition(ResolvedEvent resolvedEvent);

    public async ValueTask<EndOfStream> GetEndOfStream(CancellationToken cancellationToken) {
        using var activity = EventuousDiagnostics.ActivitySource
            .StartActivity(ActivityKind.Internal)
            ?.SetTag("stream", streamName);

        try {
            var read = Read(cancellationToken);

            var events = await read.ToArrayAsync(cancellationToken).NoContext();

            activity?.SetActivityStatus(ActivityStatus.Ok());

            return new(subscriptionId, GetLastPosition(events[0]), events[0].Event.Created);
        } catch (StreamNotFoundException) {
            activity?.SetActivityStatus(ActivityStatus.Ok());

            return new(subscriptionId, 0, DateTime.MinValue);
        } catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));

            throw;
        }
    }
}
