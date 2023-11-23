// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;

namespace Eventuous.Sql.Base;

/// <summary>
/// Represents an event as stored in a relational database.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record struct PersistedEvent(
    Guid     MessageId,
    string   MessageType,
    int      StreamPosition,
    long     GlobalPosition,
    string   JsonData,
    string?  JsonMetadata,
    DateTime Created,
    string?  StreamName
);
