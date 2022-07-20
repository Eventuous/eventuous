// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Google.Protobuf.Collections;

namespace Eventuous.GooglePubSub.Producers;

/// <summary>
/// Google PubSub produce options, supplied per message or batch
/// </summary>
[PublicAPI]
public class PubSubProduceOptions {
    /// <summary>
    /// Function, which can be used to add custom message attributes
    /// </summary>
    public Func<object, MapField<string, string>>? AddAttributes { get; init; }

    /// <summary>
    /// Optional ordering key. It only works if the publishing client is configured to support ordering.
    /// </summary>
    public string? OrderingKey { get; init; }
}