// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.GooglePubSub;

[PublicAPI]
public class PubSubAttributes {
    public string EventType   { get; set; } = "eventType";
    public string ContentType { get; set; } = "contentType";
    public string MessageId   { get; set; } = "messageId";
}
