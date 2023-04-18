// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Google.Protobuf.Collections;
using static Google.Cloud.PubSub.V1.SubscriberClient;

namespace Eventuous.GooglePubSub.Subscriptions;

using Shared;

/// <summary>
/// Google PubSub subscription service
/// </summary>
[PublicAPI]
public class GooglePubSubSubscription : EventSubscription<PubSubSubscriptionOptions> {
    public delegate ValueTask<Reply> HandleEventProcessingFailure(SubscriberClient client, PubsubMessage pubsubMessage, Exception exception);

    readonly HandleEventProcessingFailure _failureHandler;
    readonly SubscriptionName             _subscriptionName;
    readonly TopicName                    _topicName;

    SubscriberClient? _client;

    /// <summary>
    /// Creates a Google PubSub subscription service
    /// </summary>
    /// <param name="projectId">GCP project ID</param>
    /// <param name="topicId">Topic where the subscription receives messages rom</param>
    /// <param name="subscriptionId">Google PubSub subscription ID (within the project), which must already exist</param>
    /// <param name="consumePipe">Consumer pipeline</param>
    /// <param name="loggerFactory">Logger factory instance</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    public GooglePubSubSubscription(
        string            projectId,
        string            topicId,
        string            subscriptionId,
        ConsumePipe       consumePipe,
        ILoggerFactory?   loggerFactory,
        IEventSerializer? eventSerializer = null
    ) : this(
        new PubSubSubscriptionOptions {
            SubscriptionId  = subscriptionId,
            ProjectId       = projectId,
            TopicId         = topicId,
            EventSerializer = eventSerializer
        },
        consumePipe,
        loggerFactory
    ) { }

    /// <summary>
    /// Creates a Google PubSub subscription service
    /// </summary>
    /// <param name="options">Subscription options <see cref="PubSubSubscriptionOptions"/></param>
    /// <param name="consumePipe">Consumer pipeline</param>
    /// <param name="loggerFactory">Logger factory instance</param>
    public GooglePubSubSubscription(PubSubSubscriptionOptions options, ConsumePipe consumePipe, ILoggerFactory? loggerFactory)
        : base(options, consumePipe, loggerFactory) {
        _failureHandler = Ensure.NotNull(options).FailureHandler ?? DefaultEventProcessingErrorHandler;

        _subscriptionName = SubscriptionName.FromProjectSubscription(
            Ensure.NotEmptyString(options.ProjectId),
            Ensure.NotEmptyString(options.SubscriptionId)
        );

        _topicName = TopicName.FromProjectTopic(options.ProjectId, Ensure.NotEmptyString(options.TopicId));

        if (options is { FailureHandler: not null, ThrowOnError: false }) Log.ThrowOnErrorIncompatible();
    }

    Task _subscriberTask = null!;

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        if (Options.CreateSubscription) {
            await CreateSubscription(_subscriptionName, _topicName, Options.ConfigureSubscription, cancellationToken).NoContext();
        }

        _client = await CreateAsync(_subscriptionName, Options.ClientCreationSettings, Options.Settings).NoContext();

        _subscriberTask = _client.StartAsync(Handle);

        async Task<Reply> Handle(PubsubMessage msg, CancellationToken ct) {
            var eventType   = msg.Attributes[Options.Attributes.EventType];
            var contentType = msg.Attributes[Options.Attributes.ContentType];

            Logger.Current = Log;

            var evt = DeserializeData(contentType, eventType, msg.Data.ToByteArray(), _topicName.TopicId);

            var ctx = new MessageConsumeContext(
                msg.MessageId,
                eventType,
                contentType,
                _topicName.TopicId,
                0,
                0,
                0,
                msg.PublishTime.ToDateTime(),
                evt,
                AsMeta(msg.Attributes),
                SubscriptionId,
                ct
            );

            try {
                await Handler(ctx).NoContext();
                return Reply.Ack;
            }
            catch (Exception ex) {
                return await _failureHandler(_client, msg, ex).NoContext();
            }
        }

        Metadata AsMeta(MapField<string, string> attributes)
            => new(attributes.ToDictionary(x => x.Key, x => (object)x.Value)!);
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        if (_client != null) await _client.StopAsync(cancellationToken).NoContext();
        await _subscriberTask.NoContext();
    }

    public async Task CreateSubscription(SubscriptionName subscriptionName, TopicName topicName, Action<Subscription>? configureSubscription, CancellationToken cancellationToken) {
        var emulator = Options.ClientCreationSettings.DetectEmulator();
        Logger.Current = Log;
        await PubSub.CreateTopic(topicName, emulator, (msg, s) => Log.InfoLog?.Log(msg, s), cancellationToken).NoContext();
        await PubSub.CreateSubscription(subscriptionName, topicName, configureSubscription, emulator, cancellationToken).NoContext();
    }

    static ValueTask<Reply> DefaultEventProcessingErrorHandler(SubscriberClient client, PubsubMessage message, Exception exception)
        => new(Reply.Nack);
}
