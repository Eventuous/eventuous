// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Kafka;

public static class KafkaHeaderKeys {
    public static string MessageTypeHeader { get; set; } = "message-type";
    public static string ContentTypeHeader { get; set; } = "content-type";
}
