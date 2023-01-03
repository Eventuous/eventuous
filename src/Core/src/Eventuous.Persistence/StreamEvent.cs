// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous; 

[PublicAPI]
public record StreamEvent(Guid Id, object? Payload, Metadata Metadata, string ContentType, long Position);