// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static TypeMapEventSource;

public static class TypeMapperExtensions {
    /// <summary>
    /// Get the type name for a given type
    /// </summary>
    /// <param name="typeMapper">Type mapper instance</param>
    /// <param name="type">Object type for which the name needs to be retrieved</param>
    /// <param name="fail">Indicates if exception should be thrown if the type is now registered</param>
    /// <returns>Type name from the map or "unknown" if the type isn't registered and <code>fail</code> is set to false</returns>
    /// <exception cref="UnregisteredTypeException">Thrown if the type isn't registered and fail is set to true</exception>
    public static string GetTypeNameByType(this ITypeMapper typeMapper, Type type, bool fail = true) {
        var typeKnown = typeMapper.TryGetTypeName(type, out var name);

        if (!typeKnown && fail) {
            Log.TypeNotMappedToName(type);

            throw new UnregisteredTypeException(type);
        }

        return name ?? ITypeMapper.UnknownType;
    }

    public static string GetTypeName(this ITypeMapper typeMapper, object o, bool fail = true) => typeMapper.GetTypeNameByType(o.GetType(), fail);

    public static string GetTypeName<T>(this ITypeMapper typeMapper, bool fail = true) => typeMapper.GetTypeNameByType(typeof(T), fail);
    
    public static bool TryGetTypeName<T>(this ITypeMapper typeMapper, [NotNullWhen(true)] out string? typeName) => typeMapper.TryGetTypeName(typeof(T), out typeName);

    /// <summary>
    /// Get the registered type for a given name 
    /// </summary>
    /// <param name="typeMapper">Type mapper instance</param>
    /// <param name="typeName">Type name for which the type needs to be returned</param>
    /// <returns>Type that matches the given name</returns>
    /// <exception cref="UnregisteredTypeException">Thrown if the type isn't registered and fail is set to true</exception>
    public static Type GetType(this ITypeMapper typeMapper, string typeName) {
        var typeKnown = typeMapper.TryGetType(typeName, out var type);

        if (!typeKnown) {
            Log.TypeNameNotMappedToType(typeName);

            throw new UnregisteredTypeException(typeName);
        }

        return type!;
    }

    public static void EnsureTypesRegistered(this ITypeMapper typeMapper, IEnumerable<Type> types) {
        foreach (var type in types) {
            typeMapper.GetTypeNameByType(type);
        }
    }
}
