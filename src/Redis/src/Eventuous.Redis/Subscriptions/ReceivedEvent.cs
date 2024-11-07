// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Redis.Subscriptions;

public record ReceivedEvent(
    Guid     MessageId,
    string   MessageType,
    long     StreamPosition,
    long     GlobalPosition,
    string   JsonData,
    string?  JsonMetadata,
    DateTime Created,
    string   StreamName
);
