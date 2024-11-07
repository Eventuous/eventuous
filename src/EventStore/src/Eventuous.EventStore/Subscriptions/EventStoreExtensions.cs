// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Eventuous.EventStore.Subscriptions; 

static class EventStoreExtensions {
    public static EventStoreClientSettings GetSettings(this EventStoreClientBase client) {
        var prop = typeof(EventStoreClientBase).GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance);

        var getter = prop!.GetGetMethod(true);
        return (EventStoreClientSettings) getter!.Invoke(client, null)!;
    }

    public static EventStoreClientSettings Copy(this EventStoreClientSettings settings)
        => new() {
            Interceptors         = settings.Interceptors,
            ChannelCredentials   = settings.ChannelCredentials,
            ConnectionName       = settings.ConnectionName,
            ConnectivitySettings = settings.ConnectivitySettings,
            DefaultCredentials   = settings.DefaultCredentials,
            LoggerFactory        = settings.LoggerFactory,
            OperationOptions     = settings.OperationOptions
        };
}