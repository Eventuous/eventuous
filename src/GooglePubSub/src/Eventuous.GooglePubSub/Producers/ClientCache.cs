// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Eventuous.GooglePubSub.Shared;

namespace Eventuous.GooglePubSub.Producers;

class ClientCache {
    readonly string                _projectId;
    readonly PubSubProducerOptions _options;

    readonly ConcurrentDictionary<string, PublisherClient> _clients = new();

    public ClientCache(PubSubProducerOptions options) {
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
                cancellationToken
            ).NoContext();
        }

        return await PublisherClient.CreateAsync(topicName, _options.ClientCreationSettings, _options.Settings)
            .NoContext();
    }

    public IEnumerable<PublisherClient> GetAllClients() => _clients.Values;
}