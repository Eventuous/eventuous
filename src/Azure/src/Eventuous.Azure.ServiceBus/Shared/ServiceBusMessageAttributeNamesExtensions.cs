// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Azure.ServiceBus.Shared;

static class ServiceBusMessageAttributeNamesExtensions {
    /// <summary>
    /// Gets reserved names for Service Bus message attributes that are directly mapped to <see cref="ServiceBusMessage"/>.
    /// </summary>
    internal static HashSet<string> ReservedNames(this ServiceBusMessageAttributeNames attributeNames) => [
        attributeNames.CorrelationId,
        attributeNames.ReplyTo,
        attributeNames.Subject,
        attributeNames.To,
        attributeNames.MessageId
    ];
}
