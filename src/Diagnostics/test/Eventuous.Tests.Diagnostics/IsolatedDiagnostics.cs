using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.Loader;
using Eventuous.Diagnostics;
using OpenTelemetry.Trace;
using EventuousTracing = Eventuous.Diagnostics.OpenTelemetry.TracerProviderBuilderExtensions;

namespace Eventuous.Tests.Diagnostics;

// Each scenario needs untouched static state; sharing it can hide initialization-order bugs.
sealed class IsolatedDiagnostics : AssemblyLoadContext, IDisposable {
    readonly Type _diagnostics;

    public IsolatedDiagnostics() : base(isCollectible: true) {
        var assembly = LoadFromAssemblyPath(typeof(EventuousDiagnostics).Assembly.Location);
        _diagnostics = assembly.GetType(typeof(EventuousDiagnostics).FullName!)!;
    }

    public ActivitySource Source => (ActivitySource)_diagnostics.GetProperty(nameof(EventuousDiagnostics.ActivitySource))!.GetValue(null)!;

    public void RemoveDummyListener() => _diagnostics.GetMethod(nameof(EventuousDiagnostics.RemoveDummyListener))!.Invoke(null, null);

    public Meter GetMeter(string name) => (Meter)_diagnostics.GetMethod(nameof(EventuousDiagnostics.GetMeter))!.Invoke(null, [name])!;

    public TracerProviderBuilder AddTracing(TracerProviderBuilder builder) {
        var assembly = LoadFromAssemblyPath(typeof(EventuousTracing).Assembly.Location);
        var type     = assembly.GetType(typeof(EventuousTracing).FullName!)!;
        return (TracerProviderBuilder)type.GetMethod(nameof(EventuousTracing.AddEventuousTracing))!.Invoke(null, [builder])!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
        => assemblyName.Name == typeof(EventuousDiagnostics).Assembly.GetName().Name
            ? _diagnostics.Assembly
            : null;

    public void Dispose() {
        var source = Source;
        RemoveDummyListener();
        source.Dispose();
        Unload();
    }
}
