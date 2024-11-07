// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public interface ITypeMapper {
    public const string UnknownType = "unknown";

    /// <summary>
    /// Try getting a type name for a given type
    /// </summary>
    /// <param name="type">Type for which the name is requested</param>
    /// <param name="typeName">Registered type name or null if the type isn't registered</param>
    /// <returns>True if the type is registered, false otherwise</returns>
    bool TryGetTypeName(Type type, [NotNullWhen(true)] out string? typeName);

    /// <summary>
    /// Try getting a registered type for a given name
    /// </summary>
    /// <param name="typeName">Known type name</param>
    /// <param name="type">Registered type for a given name or null if the type name isn't registered</param>
    /// <returns>True if the type is registered, false otherwise</returns>
    bool TryGetType(string typeName, [NotNullWhen(true)] out Type? type);
}

public interface ITypeMapperExt : ITypeMapper {
    IEnumerable<(string TypeName, Type Type)> GetRegisteredTypes();
}