using System.Collections.Concurrent;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Eventuous.Producers.GooglePubSub;

class ClientCache {
    readonly string                _projectId;
    readonly PubSubProducerOptions _options;
    readonly ILogger?              _log;

    readonly ConcurrentDictionary<string, PublisherClient> _clients = new();

    public ClientCache(PubSubProducerOptions options, ILogger? log) {
        _projectId = Ensure.NotEmptyString(options.ProjectId, nameof(options.ProjectId));
        _options   = options;
        _log       = log;
    }

    public async Task<PublisherClient> GetOrAddPublisher(string topic, CancellationToken cancellationToken) {
        if (_clients.TryGetValue(topic, out var client)) return client;

        client = await CreateTopicAndClient(topic, cancellationToken).NoContext();
        _clients.TryAdd(topic, client);
        return client;
    }

    async Task<PublisherClient> CreateTopicAndClient(string topicId, CancellationToken cancellationToken) {
        var publisherServiceApiClient = await PublisherServiceApiClient.CreateAsync(cancellationToken).NoContext();

        var topicName = TopicName.FromProjectTopic(_projectId, topicId);

        try {
            _log?.LogInformation("Checking topic {Topic}", topicName);
            await publisherServiceApiClient.CreateTopicAsync(topicName, cancellationToken).NoContext();
            _log?.LogInformation("Created topic {Topic}", topicName);
        }
        catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists) {
            _log?.LogInformation("Topic {Topic} exists", topicName);
        }

        return await PublisherClient.CreateAsync(topicName, _options.ClientCreationSettings, _options.Settings).NoContext();
    }

    public IEnumerable<PublisherClient> GetAllClients() => _clients.Values;
}