// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Filters;

using Consumers;

public static class ConsumePipeExtensions {
    public static ConsumePipe AddDefaultConsumer(this ConsumePipe consumePipe, params IEventHandler[] handlers)
        => consumePipe.AddFilterLast(new ConsumerFilter(new DefaultConsumer(handlers)));
}
