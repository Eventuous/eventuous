// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

public static class ConsumePipeExtensions {
    public static ConsumePipe AddSystemEventsFilter(this ConsumePipe pipe)
        => pipe.AddFilterLast(new MessageFilter(x => !x.MessageType.StartsWith("$")));
}
