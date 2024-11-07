// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace Eventuous.Subscriptions.Checkpoints;

[PublicAPI]
[StructLayout(LayoutKind.Auto)]
public record struct Checkpoint(string Id, ulong? Position) {
    public static Checkpoint Empty(string id) => new(id, null);

    public readonly bool IsEmpty => Position == null;
}
