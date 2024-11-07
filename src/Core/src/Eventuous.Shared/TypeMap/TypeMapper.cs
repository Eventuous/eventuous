// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using System.Runtime.CompilerServices;
using Eventuous.Extensions.AspNetCore;

// ReSharper disable InvertIf

namespace Eventuous;

using static TypeMapEventSource;

/// <summary>
/// The TypeMap maintains event type names for known event types so we avoid using CLR type names
/// as event types. This way, we can rename event classes without breaking deserialization.
/// </summary>
public static class TypeMap {
    public static readonly TypeMapper Instance = new();

    public static string GetTypeName(object o, bool fail = true) => Instance.GetTypeName(o, fail);

    /// <summary>
    /// Registers all event types, which are decorated with <see cref="EventTypeAttribute"/>.
    /// </summary>
    /// <param name="assemblies">Zero or more assemblies that contain event classes to scan.
    /// If omitted, all the assemblies of the current <seealso cref="AppDomain"/> will be scanned.</param>
    public static void RegisterKnownEventTypes(params Assembly[] assemblies) {
        Instance.RegisterKnownEventTypes(assemblies);
    }
}

/// <summary>
/// The actual mapper behind static <see cref="TypeMap"/>.
/// </summary>
public class TypeMapper : ITypeMapperExt {
    readonly Dictionary<string, Type> _reverseMap = new();
    readonly Dictionary<Type, string> _map        = new();

    public IReadOnlyDictionary<string, Type> ReverseMap => _reverseMap;

    /// <inheritdoc />>
    public bool TryGetTypeName(Type type, [NotNullWhen(true)] out string? typeName) => _map.TryGetValue(type, out typeName);

    /// <inheritdoc />>
    public bool TryGetType(string typeName, [NotNullWhen(true)] out Type? type) => _reverseMap.TryGetValue(typeName, out type);

    public IEnumerable<(string TypeName, Type Type)> GetRegisteredTypes() => _reverseMap.Select(x => (x.Key, x.Value));

    /// <summary>
    /// Adds a message type to the map.
    /// </summary>
    /// <param name="name">Message type name. It can be omitted if the message is decorated with <see cref="EventTypeAttribute"/></param>
    /// <exception cref="ArgumentException">Thrown if there's no name provided and there's no event type attribute</exception>
    public void AddType<T>(string? name = null) => AddType(typeof(T), name);

    /// <summary>
    /// Adds a message type to the map.
    /// </summary>
    /// <param name="type">Message type</param>
    /// <param name="name">Message type name. It can be omitted if the message is decorated with <see cref="EventTypeAttribute"/></param>
    /// <exception cref="ArgumentException">Thrown if there's no name provided and there's no event type attribute</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void AddType(Type type, string? name = null) {
        if (_map.TryGetValue(type, out var registeredName)) {
            if (registeredName != name) {
                throw new ArgumentException($"Type {type.FullName} is already registered with a different name {registeredName}", nameof(name));
            }

            Log.TypeAlreadyRegistered(type.Name, name);

            return;
        }

        var attr = type.GetAttribute<EventTypeAttribute>();

        if (attr == null && name == null) {
            throw new ArgumentException($"Type {type.FullName} is not decorated with {nameof(EventTypeAttribute)} and name is not provided", nameof(name));
        }

        var eventTypeName = name ?? attr!.EventType;

        _reverseMap[eventTypeName] = type;
        _map[type]                 = eventTypeName;

        Log.TypeMapRegistered(type.Name, eventTypeName);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void RemoveType<T>() {
        var name = this.GetTypeName<T>();

        _reverseMap.Remove(name);
        _map.Remove(typeof(T));
    }

    public void RegisterKnownEventTypes(params Assembly[] assembliesWithEvents) {
        var assembliesToScan = assembliesWithEvents.Length == 0 ? GetDefaultAssemblies() : assembliesWithEvents;

        foreach (var assembly in assembliesToScan) {
            RegisterAssemblyEventTypes(assembly);
        }

        return;

        Assembly[] GetDefaultAssemblies() {
            var firstLevel = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic && NamePredicate(x.GetName())).ToArray();

            return firstLevel.SelectMany(Get).Concat(firstLevel).Distinct().ToArray();

            IEnumerable<Assembly> Get(Assembly assembly) {
                // ReSharper disable once ConvertClosureToMethodGroup
                var referenced = assembly.GetReferencedAssemblies().Where(name => NamePredicate(name));
                var assemblies = referenced.Select(Assembly.Load).ToList();

                return assemblies.Concat(assemblies.SelectMany(Get)).Distinct();
            }
        }

        bool NamePredicate(AssemblyName name)
            => name.Name != null                    &&
                !name.Name.StartsWith("System.")    &&
                !name.Name.StartsWith("Microsoft.") &&
                !name.Name.StartsWith("netstandard");
    }

    static readonly Type AttributeType = typeof(EventTypeAttribute);

    void RegisterAssemblyEventTypes(Assembly assembly) {
        var decoratedTypes = assembly.DefinedTypes.Where(x => x.IsClass && x.CustomAttributes.Any(a => a.AttributeType == AttributeType));

        foreach (var type in decoratedTypes) {
            var attr = type.GetAttribute<EventTypeAttribute>()!;
            AddType(type, attr.EventType);
        }
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class EventTypeAttribute(string eventType) : Attribute {
    public string EventType { get; } = eventType;
}

public class UnregisteredTypeException : Exception {
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public UnregisteredTypeException(Type type) : base($"Type {type.Name} is not registered in the type map") { }

    public UnregisteredTypeException(string type) : base($"Type name {type} is not registered in the type map") { }
}
