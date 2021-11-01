using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Eventuous.Diagnostics;

public static class SharedDiagnostics {
    static readonly AssemblyName AssemblyName = typeof(SharedDiagnostics).Assembly.GetName();
    static readonly Version?     Version      = AssemblyName.Version;
    
    public const string InstrumentationName = "Eventuous";

    public static readonly ActivitySource ActivitySource = new(InstrumentationName, Version?.ToString());

    public static readonly Meter Meter = new(InstrumentationName, AssemblyName.Version?.ToString());
}