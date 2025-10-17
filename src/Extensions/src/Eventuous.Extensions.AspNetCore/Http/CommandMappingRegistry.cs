// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Routing;

namespace Eventuous.Extensions.AspNetCore.Http;

/// <summary>
/// A registry to hold generated HTTP command mapping actions per assembly.
/// Source generator will populate the registry at module initialization time
/// in the consumer assembly by calling Register* methods.
/// </summary>
public static class CommandMappingRegistry {
    public readonly record struct Unbound(Type CommandType, string? Route, string? Policy);

    static readonly ConcurrentDictionary<Type, List<Action<IEndpointRouteBuilder>>> PerState     = new();
    static readonly List<Action<IEndpointRouteBuilder>>                             All          = [];
    static readonly List<Unbound>                                                   WithoutState = [];

    public static void RegisterForState(Type stateType, Action<IEndpointRouteBuilder> map) {
        var list = PerState.GetOrAdd(stateType, _ => []);

        lock (list) {
            list.Add(map);
        }
    }

    public static void RegisterAll(Action<IEndpointRouteBuilder> map) {
        lock (All) {
            All.Add(map);
        }
    }

    public static void RegisterWithoutState(Type commandType, string? route, string? policy) {
        lock (WithoutState) {
            WithoutState.Add(new(commandType, route, policy));
        }
    }

    public static IEnumerable<Action<IEndpointRouteBuilder>> GetForState(Type stateType) {
        if (!PerState.TryGetValue(stateType, out var list)) yield break;

        List<Action<IEndpointRouteBuilder>> copy;
        lock (list) copy = list.ToList();

        foreach (var a in copy) {
            yield return a;
        }
    }

    public static IEnumerable<Action<IEndpointRouteBuilder>> GetAll() {
        List<Action<IEndpointRouteBuilder>> copy;
        lock (All) copy = All.ToList();

        foreach (var a in copy) {
            yield return a;
        }
    }

    public static IEnumerable<Unbound> GetWithoutState() {
        Unbound[] copy;
        lock (WithoutState) copy = WithoutState.ToArray();

        foreach (var u in copy) {
            yield return u;
        }
    }
}
