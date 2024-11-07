// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.GooglePubSub.Subscriptions;

using static GooglePubSubSubscription;

[PublicAPI]
public record PubSubSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Google Cloud project id
    /// </summary>
    public string ProjectId { get; set; } = null!;

    /// <summary>
    /// PubSub topic id
    /// </summary>
    public string TopicId { get; set; } = null!;

    /// <summary>
    /// The subscription will try creating a PubSub subscription by default, but it requires extended permissions for PubSub.
    /// If you run the application with lower permissions, you can pre-create the subscription using your DevOps tools,
    /// then set this option to false
    /// </summary>
    public bool CreateSubscription { get; set; } = true;

    /// <summary>
    /// Configure the <seealso cref="SubscriberClientBuilder"/> before the <seealso cref="SubscriberClient"/> is created
    /// </summary>
    public Action<SubscriberClientBuilder>? ConfigureClientBuilder { get; set; }

    /// <summary>
    /// Custom failure handler, which allows overriding the default behaviour (NACK)
    /// </summary>
    public HandleEventProcessingFailure? FailureHandler { get; set; }

    /// <summary>
    /// A function to customize the subscription options when the subscription is created
    /// </summary>
    public Action<Subscription>? ConfigureSubscription { get; set; }

    /// <summary>
    /// Message attributes for system values like content type and event type
    /// </summary>
    public PubSubAttributes Attributes { get; set; } = new();
}
