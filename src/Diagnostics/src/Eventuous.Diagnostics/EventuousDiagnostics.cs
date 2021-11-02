using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Eventuous.Diagnostics;

public static class EventuousDiagnostics  {
    static readonly AssemblyName AssemblyName = typeof(EventuousDiagnostics).Assembly.GetName();
    static readonly Version?     Version      = AssemblyName.Version;
    
    public const string InstrumentationName = "eventuous";

    public static readonly ActivitySource ActivitySource = new(InstrumentationName, Version?.ToString());

    public static Meter GetMeter(string category) => new(GetMeterName(category), AssemblyName.Version?.ToString());

    public static string GetMeterName(string category) => $"{InstrumentationName}.{category}";
}