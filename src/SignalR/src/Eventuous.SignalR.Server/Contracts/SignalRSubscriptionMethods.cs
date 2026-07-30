// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SignalR;

/// <summary>
/// SignalR hub method names shared between client and server.
/// </summary>
public static class SignalRSubscriptionMethods {
    /// <summary>Server method invoked by the client to subscribe to a stream.</summary>
    public const string Subscribe = "SubscribeToStream";

    /// <summary>Server method invoked by the client to unsubscribe from a stream.</summary>
    public const string Unsubscribe = "UnsubscribeFromStream";

    /// <summary>Client method invoked by the server to deliver a stream event.</summary>
    public const string StreamEvent = "StreamEvent";

    /// <summary>Client method invoked by the server to notify about a subscription error.</summary>
    public const string StreamError = "StreamError";
}
