using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Eventuous.Producers.GooglePubSub {
    class ClientCache {
        readonly string                 _projectId;
        readonly PubSubProducerOptions? _options;
        readonly ILogger?               _log;

        readonly ConcurrentDictionary<string, PublisherClient> _clients = new();

        public ClientCache(string projectId, PubSubProducerOptions? options, ILogger? log) {
            _projectId = projectId;
            _options   = options;
            _log       = log;
        }

        public async Task<PublisherClient> GetOrAddPublisher(string topic) {
            if (_clients.TryGetValue(topic, out var client)) return client;

            client = await CreateTopicAndClient(topic);
            _clients.TryAdd(topic, client);
            return client;
        }

        async Task<PublisherClient> CreateTopicAndClient(string topicId) {
            var publisherServiceApiClient = await PublisherServiceApiClient.CreateAsync();
            var topicName                 = TopicName.FromProjectTopic(_projectId, topicId);

            try {
                _log?.LogInformation("Checking topic {Topic}", topicName);
                await publisherServiceApiClient.CreateTopicAsync(topicName);
                _log?.LogInformation("Created topic {Topic}", topicName);
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists) {
                _log?.LogInformation("Topic {Topic} exists", topicName);
            }

            return await PublisherClient.CreateAsync(topicName, _options?.ClientCreationSettings, _options?.Settings);
        }

        public IEnumerable<PublisherClient> GetAllClients() => _clients.Values;
    }
}