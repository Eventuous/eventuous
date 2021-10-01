using Google.Api.Gax;
using Eventuous.Subscriptions;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using static Google.Cloud.PubSub.V1.SubscriberClient;

namespace Eventuous.GooglePubSub.Subscriptions;

/// <summary>
/// Google PubSub subscription service
/// </summary>
[PublicAPI]
public class GooglePubSubSubscription : SubscriptionService, ICanStop {
    public delegate ValueTask<Reply> HandleEventProcessingFailure(
        SubscriberClient client,
        PubsubMessage    pubsubMessage,
        Exception        exception
    );

    readonly PubSubSubscriptionOptions    _options;
    readonly HandleEventProcessingFailure _failureHandler;
    readonly SubscriptionName             _subscriptionName;
    readonly TopicName                    _topicName;

    SubscriberClient?    _client;
    MetricServiceClient? _metricClient;

    /// <summary>
    /// Creates a Google PubSub subscription service
    /// </summary>
    /// <param name="projectId">GCP project ID</param>
    /// <param name="topicId"></param>
    /// <param name="subscriptionId">Google PubSub subscription ID (within the project), which must already exist</param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    /// <param name="measure">Callback for measuring the subscription gap</param>
    public GooglePubSubSubscription(
        string                     projectId,
        string                     topicId,
        string                     subscriptionId,
        IEnumerable<IEventHandler> eventHandlers,
        IEventSerializer?          eventSerializer = null,
        ILoggerFactory?            loggerFactory   = null,
        ISubscriptionGapMeasure?   measure         = null
    ) : this(
        new PubSubSubscriptionOptions {
            SubscriptionId = subscriptionId,
            ProjectId      = projectId,
            TopicId        = topicId
        },
        eventHandlers,
        eventSerializer,
        loggerFactory,
        measure
    ) { }

    /// <summary>
    /// Creates a Google PubSub subscription service
    /// </summary>
    /// <param name="options">Subscription options <see cref="PubSubSubscriptionOptions"/></param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    /// <param name="measure">Callback for measuring the subscription gap</param>
    public GooglePubSubSubscription(
        PubSubSubscriptionOptions  options,
        IEnumerable<IEventHandler> eventHandlers,
        IEventSerializer?          eventSerializer = null,
        ILoggerFactory?            loggerFactory   = null,
        ISubscriptionGapMeasure?   measure         = null
    ) : base(
        options,
        new NoOpCheckpointStore(),
        eventHandlers,
        eventSerializer,
        loggerFactory,
        measure
    ) {
        _options = Ensure.NotNull(options, nameof(options));

        _failureHandler = Ensure.NotNull(options, nameof(options)).FailureHandler
                       ?? DefaultEventProcessingErrorHandler;

        _subscriptionName = SubscriptionName.FromProjectSubscription(
            Ensure.NotEmptyString(options.ProjectId, nameof(options.ProjectId)),
            Ensure.NotEmptyString(options.SubscriptionId, nameof(options.SubscriptionId))
        );

        _topicName = TopicName.FromProjectTopic(
            options.ProjectId,
            Ensure.NotEmptyString(options.TopicId, nameof(options.TopicId))
        );

        _undeliveredCountRequest = GetFilteredRequest(PubSubMetricUndeliveredMessagesCount);
        _oldestAgeRequest        = GetFilteredRequest(PubSubMetricOldestUnackedMessageAge);

        ListTimeSeriesRequest GetFilteredRequest(string metric)
            => new() {
                Name = $"projects/{options.ProjectId}",
                Filter = $"metric.type = \"pubsub.googleapis.com/subscription/{metric}\" "
                       + $"AND resource.label.subscription_id = \"{options.SubscriptionId}\""
            };
    }

    Task _subscriberTask = null!;

    protected override async Task<EventSubscription> Subscribe(
        Checkpoint        checkpoint,
        CancellationToken cancellationToken
    ) {
        await CreateSubscription(
                _subscriptionName,
                _topicName,
                _options.ConfigureSubscription,
                cancellationToken
            )
            .NoContext();

        _client = await CreateAsync(
                _subscriptionName,
                _options.ClientCreationSettings,
                _options.Settings
            )
            .NoContext();

        var emulationEnabled =
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST"));

        if (!emulationEnabled)
            _metricClient = await MetricServiceClient.CreateAsync(cancellationToken).NoContext();

        _subscriberTask = _client.StartAsync(Handle);
        return new EventSubscription(SubscriptionId, this);

        async Task<Reply> Handle(PubsubMessage msg, CancellationToken ct) {
            var eventType   = msg.Attributes[_options.Attributes.EventType];
            var contentType = msg.Attributes[_options.Attributes.ContentType];

            var evt = DeserializeData(
                contentType,
                eventType,
                msg.Data.ToByteArray(),
                _topicName.TopicId
            );

            var receivedEvent = new ReceivedEvent(
                msg.MessageId,
                eventType,
                contentType,
                0,
                0,
                _topicName.TopicId,
                0,
                msg.PublishTime.ToDateTime(),
                evt,
                AsMeta(msg.Attributes)
            );

            try {
                await Handler(receivedEvent, ct).NoContext();
                return Reply.Ack;
            }
            catch (Exception ex) {
                return await _failureHandler(_client, msg, ex).NoContext();
            }
        }

        Metadata AsMeta(MapField<string, string> attributes)
            => new(attributes.ToDictionary(x => x.Key, x => (object)x.Value));
    }

    const string PubSubMetricUndeliveredMessagesCount = "num_undelivered_messages";
    const string PubSubMetricOldestUnackedMessageAge  = "oldest_unacked_message_age";

    readonly ListTimeSeriesRequest _undeliveredCountRequest;
    readonly ListTimeSeriesRequest _oldestAgeRequest;

    protected override async Task<EventPosition> GetLastEventPosition(
        CancellationToken cancellationToken
    ) {
        // Subscription metrics are sampled each 60 sec, so we need to use an extended period
        var interval = new TimeInterval {
            StartTime = Timestamp.FromDateTime(DateTime.UtcNow - TimeSpan.FromMinutes(2)),
            EndTime   = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var undelivered = await GetPoint(_undeliveredCountRequest).NoContext();
        var oldestAge   = await GetPoint(_oldestAgeRequest).NoContext();

        var age = oldestAge == null ? DateTime.UtcNow
            : DateTime.UtcNow.AddSeconds(-oldestAge.Value.Int64Value);

        return new EventPosition((ulong?)undelivered?.Value?.Int64Value, age);

        async Task<Point?> GetPoint(ListTimeSeriesRequest request) {
            request.Interval = interval;

            var result = _metricClient!.ListTimeSeriesAsync(request);
            var page   = await result.ReadPageAsync(1, cancellationToken).NoContext();
            return page.FirstOrDefault()?.Points?.FirstOrDefault();
        }
    }

    public async Task Stop(CancellationToken cancellationToken = default) {
        if (_client != null) await _client.StopAsync(cancellationToken).NoContext();

        await _subscriberTask.NoContext();
    }

    public async Task CreateSubscription(
        SubscriptionName      subscriptionName,
        TopicName             topicName,
        Action<Subscription>? configureSubscription,
        CancellationToken     cancellationToken
    ) {
        var subscriberServiceApiClient =
            await new SubscriberServiceApiClientBuilder {
                    EmulatorDetection = _options.ClientCreationSettings?.EmulatorDetection ?? EmulatorDetection.None
                }
                .BuildAsync(cancellationToken)
                .NoContext();

        var publisherServiceApiClient =
            await new PublisherServiceApiClientBuilder {
                    EmulatorDetection = _options.ClientCreationSettings?.EmulatorDetection ?? EmulatorDetection.None
                }
                .BuildAsync(cancellationToken)
                .NoContext();

        try {
            Log?.LogInformation("Checking topic {Topic}", topicName);
            await publisherServiceApiClient.CreateTopicAsync(topicName).NoContext();
            Log?.LogInformation("Created topic {Topic}", topicName);
        }
        catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists) {
            Log?.LogInformation("Topic {Topic} exists", topicName);
        }

        try {
            Log?.LogInformation(
                "Checking subscription {Subscription} for {Topic}",
                subscriptionName,
                topicName
            );

            var subscriptionRequest = new Subscription { AckDeadlineSeconds = 60 };

            configureSubscription?.Invoke(subscriptionRequest);
            subscriptionRequest.SubscriptionName = subscriptionName;
            subscriptionRequest.TopicAsTopicName = topicName;

            await subscriberServiceApiClient.CreateSubscriptionAsync(
                    subscriptionRequest
                )
                .NoContext();

            Log?.LogInformation(
                "Created subscription {Subscription} for {Topic}",
                subscriptionName,
                topicName
            );
        }
        catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists) {
            Log?.LogInformation(
                "Subscription {Subscription} for {Topic} exists",
                subscriptionName,
                topicName
            );
        }
    }

    static ValueTask<Reply> DefaultEventProcessingErrorHandler(
        SubscriberClient client,
        PubsubMessage    message,
        Exception        exception
    )
        => new(Reply.Nack);
}