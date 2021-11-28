using static Google.Cloud.PubSub.V1.PublisherClient;

namespace Eventuous.GooglePubSub.Producers; 

[PublicAPI]
public class PubSubProducerOptions {
    /// <summary>
    /// Google Cloud project id
    /// </summary>
    public string ProjectId { get; init; } = null!;
        
    /// <summary>
    /// <see cref="ClientCreationSettings"/> for the <seealso cref="Publisher.PublisherClient"/> creation
    /// </summary>
    public ClientCreationSettings? ClientCreationSettings { get; init; }

    /// <summary>
    /// <see cref="PublisherClient.Settings"/> of the <seealso cref="PublisherClient"/>
    /// </summary>
    public Settings? Settings { get; init; }

    /// <summary>
    /// Message attributes for system values like content type and event type
    /// </summary>
    public PubSubAttributes Attributes { get; init; } = new();
}