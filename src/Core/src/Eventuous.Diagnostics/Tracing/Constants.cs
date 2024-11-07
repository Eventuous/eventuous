// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics.Tracing;

public static class Constants {
    public static class Components {
        public const string CommandService = "service";
        public const string EventStore     = "eventstore";
        public const string Subscription   = "sub";
        public const string Consumer       = "consumer";
        public const string EventHandler   = "handler";
    }

    public static class Operations {
        public const string StreamExists   = "exists";
        public const string AppendEvents   = "append";
        public const string ReadEvents     = "read";
        public const string TruncateStream = "truncate";
        public const string DeleteStream   = "delete";
    }
}
