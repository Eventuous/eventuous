using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Eventuous.Diagnostics;

public static class EventuousDiagnostics {
    static readonly AssemblyName AssemblyName = typeof(Metadata).Assembly.GetName();
    static readonly Version?     Version      = AssemblyName.Version;

    static EventuousDiagnostics() => Enabled = Environment.GetEnvironmentVariable("EVENTUOUS_DISABLE_DIAGS") != "1";

    public const string InstrumentationName = DiagnosticName.BaseName;

    static ActivitySource?   activitySource;
    static ActivityListener? listener;

    public static bool Enabled { get; }

    public static ActivitySource ActivitySource {
        get {
            if (activitySource != null) return activitySource;
            activitySource = new ActivitySource(InstrumentationName, Version?.ToString());

            listener = DummyActivityListener.Create();
            ActivitySource.AddActivityListener(listener);
            return activitySource;
        }
    }

    public static void RemoveDummyListener() => listener?.Dispose();

    public static Meter GetCategoryMeter(string category)
        => new(GetMeterName(category), AssemblyName.Version?.ToString());

    public static Meter GetMeter(string name) => new(name, AssemblyName.Version?.ToString());

    public static string GetMeterName(string category) => $"{InstrumentationName}.{category}";
}