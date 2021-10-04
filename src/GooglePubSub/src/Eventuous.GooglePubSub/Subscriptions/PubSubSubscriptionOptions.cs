using Eventuous.Subscriptions;
using static Eventuous.GooglePubSub.Subscriptions.GooglePubSubSubscription;
using static Google.Cloud.PubSub.V1.SubscriberClient;

namespace Eventuous.GooglePubSub.Subscriptions; 

[PublicAPI]
public record PubSubSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Google Cloud project id
    /// </summary>
    public string ProjectId { get; init; } = null!;

    /// <summary>
    /// PubSub topic id
    /// </summary>
    public string TopicId { get; init; } = null!;
    
    /// <summary>
    /// Set to true to enable subscription monitoring using <see cref="ISubscriptionGapMeasure"/>
    /// Disabled by default as you can monitor subscriptions using Google Cloud native monitoring tools
    /// </summary>
    public bool EnableMonitoring { get; init; }

    /// <summary>
    /// <see cref="ClientCreationSettings"/> for the <seealso cref="SubscriberClient"/> creation
    /// </summary>
    public ClientCreationSettings? ClientCreationSettings { get; init; }

    /// <summary>
    /// <see cref="Settings"/> of the <seealso cref="SubscriberClient"/>
    /// </summary>
    public Settings? Settings { get; init; }

    /// <summary>
    /// Custom failure handler, which allows overriding the default behaviour (NACK)
    /// </summary>
    public HandleEventProcessingFailure? FailureHandler { get; init; }
        
    /// <summary>
    /// A function to customise the subscription options when the subscription is created
    /// </summary>
    public Action<Subscription>? ConfigureSubscription { get; init; }

    /// <summary>
    /// Message attributes for system values like content type and event type
    /// </summary>
    public PubSubAttributes Attributes { get; init; } = new();
}