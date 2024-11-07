// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;

namespace Eventuous.Producers.Diagnostics;

public static class ProducerActivity {
    public static (Activity? act, ProducedMessage msgs) Start(ProducedMessage message, IEnumerable<KeyValuePair<string, object?>>? tags) {
        var activity  = GetActivity(tags);
        var meta      = GetMeta(message.Metadata);
        var messageId = message.MessageId.ToString();

        activity?
            .SetTag(TelemetryTags.Message.Id, messageId)
            .SetTag(TelemetryTags.Messaging.MessageId, messageId)
            .CopyParentTag(TelemetryTags.Messaging.ConversationId)
            .SetOrCopyParentTag(
                TelemetryTags.Messaging.CausationId,
                meta.GetCausationId(),
                TelemetryTags.Message.Id
            )
            .SetOrCopyParentTag(TelemetryTags.Messaging.CorrelationId, meta.GetCorrelationId())
            .Start();

        meta.AddActivityTags(activity);

        return (activity, message with { Metadata = meta });
    }

    public static (Activity? act, IEnumerable<ProducedMessage> msgs) Start(
            IEnumerable<ProducedMessage>                messages,
            IEnumerable<KeyValuePair<string, object?>>? tags
        ) {
        var activity = GetActivity(tags);

        activity?
            .CopyParentTag(TelemetryTags.Messaging.ConversationId)
            .CopyParentTag(TelemetryTags.Messaging.CausationId, TelemetryTags.Message.Id)
            .CopyParentTag(TelemetryTags.Messaging.CorrelationId)
            .Start();

        return (activity, messages.Select(GetMessage));

        ProducedMessage GetMessage(ProducedMessage message)
            => message with { Metadata = GetMeta(message.Metadata).AddActivityTags(activity) };
    }

    static Activity? GetActivity(IEnumerable<KeyValuePair<string, object?>>? tags)
        => EventuousDiagnostics.ActivitySource.CreateActivity(
            "produce",
            ActivityKind.Producer,
            parentContext: default,
            tags,
            idFormat: ActivityIdFormat.W3C
        );

    static Metadata GetMeta(Metadata? metadata)
        => Metadata
            .FromMeta(metadata)
            .WithCorrelationId(metadata?.GetCorrelationId());
}
