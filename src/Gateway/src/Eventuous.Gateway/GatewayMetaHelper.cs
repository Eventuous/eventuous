// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

static class GatewayMetaHelper {
    public static Metadata GetMeta<T>(this GatewayMessage<T> gatewayMessage, IMessageConsumeContext context) {
        var (_, _, metadata, _) = gatewayMessage;
        var meta = metadata == null ? new() : new Metadata(metadata);
        return meta.WithCausationId(context.MessageId);
    }

    public static Metadata GetContextMeta(IMessageConsumeContext context) {
        var headers = new Dictionary<string, object?> {
            [GatewayContextItems.OriginalMessage]        = context.Message,
            [GatewayContextItems.OriginalStream]         = context.Stream,
            [GatewayContextItems.OriginalStreamPosition] = context.StreamPosition,
            [GatewayContextItems.OriginalGlobalPosition] = context.GlobalPosition,
            [GatewayContextItems.OriginalMessageId]      = context.MessageId,
            [GatewayContextItems.OriginalMessageType]    = context.MessageType,
            [GatewayContextItems.OriginalMessageMeta]    = context.Metadata
        };

        return new(headers);
    }
}

[PublicAPI]
public static class ProducedMessageExtensions {
    extension(ProducedMessage message) {
        public StreamName? GetOriginalStream()
            => message.AdditionalHeaders?.Get<StreamName>(GatewayContextItems.OriginalStream);

        public object? GetOriginalMessage()
            => message.AdditionalHeaders?.Get<object>(GatewayContextItems.OriginalMessage);

        public Metadata? GetOriginalMetadata()
            => message.AdditionalHeaders?.Get<Metadata>(GatewayContextItems.OriginalMessageMeta);

        public ulong GetOriginalStreamPosition()
            => message.AdditionalHeaders?.Get<ulong>(GatewayContextItems.OriginalStreamPosition) ?? default;

        public ulong GetOriginalGlobalPosition()
            => message.AdditionalHeaders?.Get<ulong>(GatewayContextItems.OriginalGlobalPosition) ?? default;

        public string? GetOriginalMessageId()
            => message.AdditionalHeaders?.Get<string>(GatewayContextItems.OriginalMessageId);

        public string? GetOriginalMessageType()
            => message.AdditionalHeaders?.Get<string>(GatewayContextItems.OriginalMessageType);
    }
}

public static class GatewayContextItems {
    public const string OriginalMessageId      = nameof(OriginalMessageId);
    public const string OriginalMessage        = nameof(OriginalMessage);
    public const string OriginalMessageType    = nameof(OriginalMessageType);
    public const string OriginalMessageMeta    = nameof(OriginalMessageMeta);
    public const string OriginalStream         = nameof(OriginalStream);
    public const string OriginalStreamPosition = nameof(OriginalStreamPosition);
    public const string OriginalGlobalPosition = nameof(OriginalGlobalPosition);
}