// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

static class GatewayMetaHelper {
    public static Metadata GetMeta<T>(this GatewayMessage<T> gatewayMessage, IMessageConsumeContext context) {
        var (_, _, metadata, _) = gatewayMessage;
        var meta = metadata == null ? new Metadata() : new Metadata(metadata);
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

        return new Metadata(headers);
    }
}

[PublicAPI]
public static class ProducedMessageExtensions {
    public static Stream? GetOriginalStream(this ProducedMessage message)
        => message.AdditionalHeaders?.Get<Stream>(GatewayContextItems.OriginalStream);

    public static object? GetOriginalMessage(this ProducedMessage message)
        => message.AdditionalHeaders?.Get<object>(GatewayContextItems.OriginalMessage);
    
    public static Metadata? GetOriginalMetadata(this ProducedMessage message)
        => message.AdditionalHeaders?.Get<Metadata>(GatewayContextItems.OriginalMessageMeta);
    
    public static ulong GetOriginalStreamPosition(this ProducedMessage message)
        => message.AdditionalHeaders?.Get<ulong>(GatewayContextItems.OriginalStreamPosition) ?? default;
    
    public static ulong GetOriginalGlobalPosition(this ProducedMessage message)
        => message.AdditionalHeaders?.Get<ulong>(GatewayContextItems.OriginalGlobalPosition) ?? default;
    
    public static string? GetOriginalMessageId(this ProducedMessage message)
        => message.AdditionalHeaders?.Get<string>(GatewayContextItems.OriginalMessageId);
    
    public static string? GetOriginalMessageType(this ProducedMessage message)
        => message.AdditionalHeaders?.Get<string>(GatewayContextItems.OriginalMessageType);
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