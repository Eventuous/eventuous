// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

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

    /// <summary>
    /// The producer will try creating a PubSub topic by default, but it requires extended permissions for PubSub.
    /// If you run the application with lower permissions, you can pre-create the topic using your DevOps tools,
    /// then set this option to false
    /// </summary>
    public bool CreateTopic { get; set; } = true;
}