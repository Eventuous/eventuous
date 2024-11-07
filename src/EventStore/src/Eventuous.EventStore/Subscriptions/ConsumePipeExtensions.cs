// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Extensions for <see cref="ConsumePipe"/>
/// </summary>
public static class ConsumePipeExtensions {
    /// <summary>
    /// Adds a filter to ignore EventStoreDB system events
    /// </summary>
    /// <param name="pipe"></param>
    /// <returns></returns>
    public static ConsumePipe AddSystemEventsFilter(this ConsumePipe pipe) => pipe.AddFilterLast(new MessageFilter(x => !x.MessageType.StartsWith("$")));
}
