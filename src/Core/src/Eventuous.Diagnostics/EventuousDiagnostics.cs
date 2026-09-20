// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Metrics;

namespace Eventuous.Diagnostics;

public static class EventuousDiagnostics {
    static EventuousDiagnostics() {
        Enabled        = Environment.GetEnvironmentVariable("EVENTUOUS_DISABLE_DIAGS") != "1";
        ActivitySource = new(InstrumentationName, InstrumentationVersion.Value);
        listener       = DummyActivityListener.Create();
        ActivitySource.AddActivityListener(listener);
    }

    public const string InstrumentationName = DiagnosticName.BaseName;

    static readonly ActivityListener listener;

    public static KeyValuePair<string, object?>[] Tags { get; private set; } = [];

    public static void AddDefaultTag(string key, object? value) {
        var tags = new List<KeyValuePair<string, object?>>(Tags) { new(key, value) };
        Tags = [.. tags];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeyValuePair<string, object?>[] CombineWithDefaultTags(params KeyValuePair<string, object?>[] tags) {
        if (Tags.Length == 0) return tags;

        var combinedTags = new KeyValuePair<string, object?>[Tags.Length + tags.Length];
        Array.Copy(Tags, combinedTags, Tags.Length);
        Array.Copy(tags, 0, combinedTags, Tags.Length, tags.Length);

        return combinedTags;
    }

    public static bool Enabled { get; private set; }

    /// <summary>
    /// Allows disabling the diagnostics from code. Normally, you disable it by setting the environment variable EVENTUOUS_DISABLE_DIAGS=1
    /// </summary>
    public static void Disable() => Enabled = false;

    public static void Enable() => Enabled = true;

    public static ActivitySource ActivitySource { get; }

    public static void RemoveDummyListener() => listener.Dispose();

    public static Meter GetMeter(string name) => new(name, InstrumentationVersion.Value);

    public static string GetMeterName(string category) => $"{InstrumentationName}.{category}";
}
