// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace Eventuous; 

[PublicAPI]
[StructLayout(LayoutKind.Auto)]
public record struct StreamEvent(Guid Id, object? Payload, Metadata Metadata, string ContentType, long Position, bool FromArchive = false);