using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public static class TypeMap {
        static readonly List<Assembly> Assemblies = new();

        static readonly Dictionary<string, Type> ReverseMap = new();
        static readonly Dictionary<Type, string> Map        = new();

        public static string GetTypeName<T>() => Map[typeof(T)];

        public static string GetTypeName(object o) => Map[o.GetType()];

        public static string GetTypeNameByType(Type type) => Map[type];

        public static Type GetType(string typeName) => ReverseMap[typeName];

        public static bool TryGetType(string typeName, out Type? type) {
            return ReverseMap.TryGetValue(typeName, out type);
        }

        public static void AddType<T>(string name) => AddType(typeof(T), name);

        static void AddType(Type type, string name) {
            ReverseMap[name] = type;
            Map[type]   = name;
        }

        public static bool IsTypeRegistered<T>() => Map.ContainsKey(typeof(T));

        public static void RegisterKnownEventTypes(params Assembly[] assemblies) {
            foreach (var assembly in assemblies) {
                RegisterAssemblyEventTypes(assembly);
            }
        }

        static Type _attributeType = typeof(EventTypeAttribute);

        static void RegisterAssemblyEventTypes(Assembly assembly) {
            var decoratedTypes = assembly.DefinedTypes.Where(
                x => x.IsClass && x.CustomAttributes.Any(a => a.AttributeType == _attributeType)
            );

            foreach (var type in decoratedTypes) {
                var attr = (EventTypeAttribute)Attribute.GetCustomAttribute(type, _attributeType)!;
                AddType(type, attr.EventType);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class EventTypeAttribute : Attribute {
        public string EventType { get; }

        public EventTypeAttribute(string eventType) => EventType = eventType;
    }
}