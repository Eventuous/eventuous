using System.Reflection;

namespace Eventuous;

/// <summary>
/// The TypeMap maintains event type names for known event types so we avoid using CLR type names
/// as event types. This way, we can rename event classes without breaking deserialization.
/// </summary>
[PublicAPI]
public static class TypeMap {
    public static readonly TypeMapper Instance = new();

    public static string GetTypeName<T>() => Instance.GetTypeName<T>();

    public static string GetTypeName(object o) => Instance.GetTypeName(o);

    public static string GetTypeNameByType(Type type) => Instance.GetTypeNameByType(type);

    public static Type GetType(string typeName) => Instance.GetType(typeName);

    public static bool TryGetType(string typeName, out Type? type) => Instance.TryGetType(typeName, out type);

    public static void AddType<T>(string name) => Instance.AddType<T>(name);

    static void AddType(Type type, string name) => Instance.AddType(type, name);

    public static bool IsTypeRegistered<T>() => Instance.IsTypeRegistered<T>();

    /// <summary>
    /// Registers all event types, which are decorated with <see cref="EventTypeAttribute"/>.
    /// </summary>
    /// <param name="assemblies">Zero or more assemblies that contain event classes to scan.
    /// If omitted, all the assemblies of the current <seealso cref="AppDomain"/> will be scanned.</param>
    public static void RegisterKnownEventTypes(params Assembly[] assemblies)
        => Instance.RegisterKnownEventTypes(assemblies);
}

/// <summary>
/// The actual mapper behind static <see cref="TypeMap"/>. Normally, you won't need to use it.
/// </summary>
public class TypeMapper {
    readonly Dictionary<string, Type> _reverseMap = new();
    readonly Dictionary<Type, string> _map        = new();

    public string GetTypeName<T>()
        => _map.TryGetValue(typeof(T), out var name) ? name : throw new UnregisteredTypeException(typeof(T));

    public string GetTypeName(object o) 
        => _map.TryGetValue(o.GetType(), out var name) ? name : throw new UnregisteredTypeException(o.GetType());

    public string GetTypeNameByType(Type type) 
        => _map.TryGetValue(type, out var name) ? name : throw new UnregisteredTypeException(type);

    public Type GetType(string typeName) 
        => _reverseMap.TryGetValue(typeName, out var type) ? type : throw new UnregisteredTypeException(typeName);

    public bool TryGetType(string typeName, out Type? type) => _reverseMap.TryGetValue(typeName, out type);

    public void AddType<T>(string name) => AddType(typeof(T), name);

    internal void AddType(Type type, string name) {
        _reverseMap[name] = type;
        _map[type]        = name;
    }

    public bool IsTypeRegistered<T>() => _map.ContainsKey(typeof(T));

    public void RegisterKnownEventTypes(params Assembly[] assemblies) {
        var assembliesToScan = assemblies.Length == 0
            ? AppDomain.CurrentDomain.GetAssemblies() : assemblies;

        foreach (var assembly in assembliesToScan) {
            RegisterAssemblyEventTypes(assembly);
        }
    }

    static readonly Type AttributeType = typeof(EventTypeAttribute);

    void RegisterAssemblyEventTypes(Assembly assembly) {
        var decoratedTypes = assembly.DefinedTypes.Where(
            x => x.IsClass && x.CustomAttributes.Any(a => a.AttributeType == AttributeType)
        );

        foreach (var type in decoratedTypes) {
            var attr = type.GetAttribute<EventTypeAttribute>()!;
            AddType(type, attr.EventType);
        }
    }

    public void EnsureTypesRegistered(IEnumerable<Type> types) {
        foreach (var type in types) {
            GetTypeNameByType(type);
        }
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class EventTypeAttribute : Attribute {
    public string EventType { get; }

    public EventTypeAttribute(string eventType) => EventType = eventType;
}

public class UnregisteredTypeException : Exception {
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public UnregisteredTypeException(Type type) : base($"Type {type.Name} is not registered in the type map") { }

    public UnregisteredTypeException(string type) : base($"Type name {type} is not registered in the type map") { }
}