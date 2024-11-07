// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace Eventuous; 

[StructLayout(LayoutKind.Auto)]
public record struct NewStreamEvent(Guid Id, object? Payload, Metadata Metadata);

[StructLayout(LayoutKind.Auto)]
public record struct StreamEvent(Guid Id, object? Payload, Metadata Metadata, string ContentType, long Position, bool FromArchive = false);