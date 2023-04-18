// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;

namespace Eventuous.GooglePubSub.Producers;

using Shared;

class ClientCache {
    readonly ILogger?              _log;
    readonly string                _projectId;
    readonly PubSubProducerOptions _options;

    readonly ConcurrentDictionary<string, PublisherClient> _clients = new();

    public ClientCache(PubSubProducerOptions options, ILogger? log) {
        _log       = log;
        _projectId = Ensure.NotEmptyString(options.ProjectId);
        _options   = Ensure.NotNull(options);
    }

    public async Task<PublisherClient> GetOrAddPublisher(string topic, CancellationToken cancellationToken) {
        if (_clients.TryGetValue(topic, out var client)) return client;

        client = await CreateTopicAndClient(topic, cancellationToken).NoContext();
        _clients.TryAdd(topic, client);
        return client;
    }

    async Task<PublisherClient> CreateTopicAndClient(string topicId, CancellationToken cancellationToken) {
        var topicName = TopicName.FromProjectTopic(_projectId, topicId);

        if (_options.CreateTopic) {
            await PubSub.CreateTopic(
                    topicName,
                    _options.ClientCreationSettings.DetectEmulator(),
                    (msg, t) => _log?.LogInformation("{Message}: {Topic}", msg, t),
                    cancellationToken
                )
                .NoContext();
        }

        return await PublisherClient.CreateAsync(topicName, _options.ClientCreationSettings, _options.Settings).NoContext();
    }

    public IEnumerable<PublisherClient> GetAllClients()
        => _clients.Values;
}
