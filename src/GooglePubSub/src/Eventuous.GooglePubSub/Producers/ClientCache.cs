// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;

namespace Eventuous.GooglePubSub.Producers;

using Shared;

class ClientCache(PubSubProducerOptions options, ILogger? log) {
    readonly string                                        _projectId = Ensure.NotEmptyString(options.ProjectId);
    readonly PubSubProducerOptions                         _options   = Ensure.NotNull(options);
    readonly ConcurrentDictionary<string, PublisherClient> _clients   = new();

    public async Task<PublisherClient> GetOrAddPublisher(string topic, CancellationToken cancellationToken) {
        if (_clients.TryGetValue(topic, out var client)) return client;

        client = await CreateTopicAndClient(topic, cancellationToken).NoContext();
        _clients.TryAdd(topic, client);

        return client;
    }

    async Task<PublisherClient> CreateTopicAndClient(string topicId, CancellationToken cancellationToken) {
        var topicName = TopicName.FromProjectTopic(_projectId, topicId);

        var builder = new PublisherClientBuilder { Logger = log };
        _options.ConfigureClientBuilder?.Invoke(builder);
        builder.TopicName = topicName;

        if (_options.CreateTopic) {
            await PubSub.CreateTopic(topicName, builder.EmulatorDetection, (msg, t) => log?.LogInformation("{Message}: {Topic}", msg, t), cancellationToken)
                .NoContext();
        }

        return await builder.BuildAsync(cancellationToken).NoContext();
    }

    public IEnumerable<PublisherClient> GetAllClients() => _clients.Values;
}
