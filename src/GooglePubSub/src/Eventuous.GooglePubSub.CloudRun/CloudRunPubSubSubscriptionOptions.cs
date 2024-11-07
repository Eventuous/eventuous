// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.GooglePubSub.CloudRun;

/// <summary>
/// Cloud Run Pub/Sub subscription options
/// </summary>
public record CloudRunPubSubSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Pub/Sub topic ID, it will only be used for informational purposes
    /// </summary>
    public string TopicId { get; set; } = "unknown";

    /// <summary>
    /// Message attribute keys for system values like content type and event type
    /// </summary>
    public PubSubAttributes Attributes { get; set; } = new();
}
