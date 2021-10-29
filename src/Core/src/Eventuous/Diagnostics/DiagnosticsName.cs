namespace Eventuous.Diagnostics; 

[AttributeUsage(AttributeTargets.Class)]
public class DiagnosticsName : Attribute {
    public DiagnosticsName(string name) => Name = name;

    public string Name { get; }
}