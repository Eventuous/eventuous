// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Eventuous.KurrentDB.Subscriptions;

static class KurrentDBExtensions {
    public static KurrentDBClientSettings GetSettings(this KurrentDBClientBase client) {
        var prop = typeof(KurrentDBClientBase).GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance);

        var getter = prop!.GetGetMethod(true);
        return (KurrentDBClientSettings) getter!.Invoke(client, null)!;
    }

    public static KurrentDBClientSettings Copy(this KurrentDBClientSettings settings)
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