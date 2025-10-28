// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eventuous.Subscriptions {
    public record CheckpointInitialPosition() {
        public static CheckpointInitialPosition From(long position) => new From(position);
        public static CheckpointInitialPosition Beginning => new Beginning();
        public static CheckpointInitialPosition End => new End();
    };
    public record From(long Position) : CheckpointInitialPosition;
    public record Beginning() : CheckpointInitialPosition;
    public record End() : CheckpointInitialPosition;
}
