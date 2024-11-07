// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace Eventuous.Producers;

using Diagnostics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct ProducedMessage {
    public ProducedMessage(object message, Metadata? metadata, Metadata? additionalHeaders = null, Guid? messageId = null) {
        Message           = message;
        Metadata          = metadata;
        AdditionalHeaders = additionalHeaders;
        MessageId         = messageId ?? Guid.NewGuid();
    }

    public object               Message           { get; }
    public Metadata?            Metadata          { get; init; }
    public Metadata?            AdditionalHeaders { get; }
    public Guid                 MessageId         { get; }
    public AcknowledgeProduce?  OnAck             { get; init; }
    public ReportFailedProduce? OnNack            { get; init; }

    public ValueTask Ack<T>() where T : class {
        ProducerEventSource<T>.Log.ProduceAcknowledged(this);

        return OnAck?.Invoke(this) ?? default;
    }

    public ValueTask Nack<T>(string message, Exception? exception) where T : class {
        ProducerEventSource<T>.Log.ProduceNotAcknowledged(this, message, exception);

        if (OnNack != null) return OnNack(this, message, exception);

        throw exception ?? new InvalidOperationException(message);
    }
}
