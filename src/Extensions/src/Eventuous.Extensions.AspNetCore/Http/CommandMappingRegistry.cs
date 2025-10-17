// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using ConfigureEndpoint = System.Action<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder>;

namespace Eventuous.Extensions.AspNetCore.Http;

/// <summary>
/// A registry to hold generated HTTP command mapping actions per assembly.
/// Source generator will populate the registry at module initialization time
/// in the consumer assembly by calling Register* methods.
/// </summary>
public static class CommandMappingRegistry {
    readonly record struct Unbound(Type CommandType, string? Route, string? Policy);

    public readonly record struct Bound(Type CommandType, ConfigureEndpoint Map);

    static readonly ConcurrentDictionary<Type, List<Bound>> PerState = new();
    static readonly List<Bound>                       All      = [];

    // TODO: Figure out what to do with it
    // ReSharper disable once CollectionNeverQueried.Local
    static readonly List<Unbound> WithoutState = [];

    public static void RegisterForState(Type stateType, Type commandType, ConfigureEndpoint map) {
        var list = PerState.GetOrAdd(stateType, _ => []);

        lock (list) {
            list.Add(new(commandType, map));
        }
    }

    public static void RegisterAll(Type commandType, ConfigureEndpoint map) {
        lock (All) {
            All.Add(new(commandType, map));
        }
    }

    public static void RegisterWithoutState(Type commandType, string? route, string? policy) {
        lock (WithoutState) {
            WithoutState.Add(new(commandType, route, policy));
        }
    }

    public static IEnumerable<Bound> GetForState(Type stateType) {
        if (!PerState.TryGetValue(stateType, out var list)) yield break;

        List<Bound> copy;
        lock (list) copy = list.ToList();

        foreach (var a in copy) {
            yield return a;
        }
    }

    public static IEnumerable<Bound> GetAll() {
        List<Bound> copy;
        lock (All) copy = All.ToList();

        foreach (var a in copy) {
            yield return a;
        }
    }
}
