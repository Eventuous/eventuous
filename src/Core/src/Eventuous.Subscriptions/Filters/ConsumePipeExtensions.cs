// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Consumers;

namespace Eventuous.Subscriptions.Filters; 

public static class ConsumePipeExtensions {
    public static ConsumePipe AddDefaultConsumer(this ConsumePipe consumePipe, params IEventHandler[] handlers)
        => consumePipe.AddFilterLast(new ConsumerFilter(new DefaultConsumer(handlers)));
}