namespace Eventuous.Connectors.Base;

public class ExporterMappings<T> {
    Dictionary<string, Action<T>> _mappings = new();

    public ExporterMappings<T> Add(string name, Action<T> configure) {
        _mappings.Add(name, configure);
        return this;
    }

    public void RegisterExporters(T provider, string[]? exporters) {
        if (exporters == null) {
            return;
        }

        foreach (var exporter in exporters) {
            if (_mappings.TryGetValue(exporter, out var addExporter)) {
                addExporter(provider);
            }
        }
    }
}
