// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Static holder for the default event serializer instance.
/// Must be configured before use — either via <see cref="SetDefault"/> or DI registration.
/// For AOT applications, use <see cref="DefaultStaticEventSerializer"/> with a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.
/// </summary>
[PublicAPI]
public static class EventSerializer {
    static IEventSerializer? _default;

    /// <summary>
    /// Gets the default event serializer. Throws if not configured.
    /// </summary>
    public static IEventSerializer Default => _default
        ?? throw new InvalidOperationException(
            "No default event serializer configured. "
          + "Call EventSerializer.SetDefault() at startup or register IEventSerializer in DI. "
          + "For AOT, use DefaultStaticEventSerializer with a JsonSerializerContext.");

    /// <summary>
    /// Sets the default event serializer instance.
    /// </summary>
    public static void SetDefault(IEventSerializer serializer)
        => _default = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <summary>
    /// Sets the default event serializer only if one has not already been configured.
    /// Returns true if the default was set, false if it was already configured.
    /// </summary>
    public static bool TrySetDefault(IEventSerializer serializer) {
        ArgumentNullException.ThrowIfNull(serializer);

        return Interlocked.CompareExchange(ref _default, serializer, null) == null;
    }
}
