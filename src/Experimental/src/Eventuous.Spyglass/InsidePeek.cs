// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Microsoft.Extensions.Logging;

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace Eventuous.Spyglass;

public class InsidePeek {
    readonly IEventStore              _eventStore;
    readonly ILogger                  _log;
    readonly AggregateFactoryRegistry _registry;

    [PublicAPI]
    public InsidePeek(IEventStore eventStore, ILogger<InsidePeek> log) : this(AggregateFactoryRegistry.Instance, eventStore, log) { }

    public InsidePeek(AggregateFactoryRegistry registry, IEventStore eventStore, ILogger<InsidePeek> log) {
        _eventStore = eventStore;
        _log        = log;
        _registry   = registry;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies) {
            Scan(assembly);
        }
    }

    void Scan(Assembly assembly) {
        if (assembly.IsDynamic) {
            return;
        }

        var aggregateType = typeof(Aggregate<>);

        var cl = assembly
            .ExportedTypes
            .Where(x => DeepBaseType(x, aggregateType) && !x.IsAbstract)
            .ToList();

        var reg = _registry.Registry;

        foreach (var type in cl) {
            var stateType = GetStateType(type);

            if (stateType == null) continue;

            var methods = (type as dynamic).DeclaredMethods as MethodInfo[];

            AggregateInfos.Add(new AggregateInfo(type, stateType, methods!, () => CreateInstance(reg, type)));
        }

        return;

        Type? GetStateType(Type type)
            => type.BaseType!.GenericTypeArguments.Length == 0
                ? null
                : type.BaseType!.GenericTypeArguments[0];
    }

    static dynamic CreateInstance(Dictionary<Type, Func<dynamic>> reg, Type aggregateType) {
        var instance = reg.TryGetValue(aggregateType, out var factory)
            ? factory()
            : Activator.CreateInstance(aggregateType)!;

        return instance;
    }

    public async Task<object> Load(string streamName, int version) {
        var typeName       = streamName[..streamName.IndexOf('-')];
        var agg            = AggregateInfos.First(x => x.AggregateType == typeName);
        var events         = await _eventStore.ReadStream(new StreamName(streamName), StreamReadPosition.Start, true, CancellationToken.None);
        var aggregate      = agg.GetAggregate();
        var selectedEvents = version == -1 ? events : events.Take(version + 1);
        aggregate.Load(selectedEvents.Select(x => x.Payload));

        return new { aggregate.State, Events = events.Select(x => new { EventType = x.Payload!.GetType().Name, x.Payload }) };
    }

    public object[] Aggregates => AggregateInfos.Select(x => x.GetInfo()).ToArray();

    List<AggregateInfo> AggregateInfos { get; } = [];

    static bool DeepBaseType(Type t, Type compareWith) {
        while (true) {
            if (t.BaseType == null) return false;
            if (t.BaseType == compareWith) return true;

            t = t.BaseType;
        }
    }

    record AggregateInfo {
        public AggregateInfo(Type aggregateType, Type stateType, MethodInfo[] methods, Func<dynamic> factory) {
            _aggregateType = aggregateType;
            _stateType     = stateType;
            _methods       = methods;
            _factory       = factory;
        }

        public dynamic GetAggregate() => _factory();

        public object GetInfo() {
            var               instance        = GetAggregate();
            object            state           = instance.State;
            var               handlers        = state.GetPrivateMember("_handlers");
            var               handlerType     = typeof(Func<,,>).MakeGenericType(_stateType, typeof(object), _stateType);
            var               handlersDicType = typeof(Dictionary<,>).MakeGenericType(typeof(Type), handlerType);
            dynamic           handlersDic     = Convert.ChangeType(handlers, handlersDicType)!;
            IEnumerable<Type> keys            = handlersDic.Keys;

            return new {
                Type      = _aggregateType.Name,
                StateType = _stateType.Name,
                Methods   = _methods.Select(x => x.Name).ToArray(),
                Events    = keys.Select(x => x.Name).ToArray()
            };
        }

        public override string ToString() => $"{_aggregateType.Name} ({_stateType.Name})";

        readonly Type          _aggregateType;
        readonly Type          _stateType;
        readonly MethodInfo[]  _methods;
        readonly Func<dynamic> _factory;

        public string AggregateType => _aggregateType.Name;
    }
}
