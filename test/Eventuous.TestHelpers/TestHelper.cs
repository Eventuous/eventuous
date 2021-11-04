#nullable enable
using System.Reflection;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;

namespace Eventuous.TestHelpers; 

public static class TestHelper {
    public static IEventHandler[]? GetNestedConsumerHandlers(this IMessageConsumer? consumer) {
        IEventHandler[]? handlers = null;

        var c = consumer;

        while (c != null) {
            handlers = GetPrivateMember<IEventHandler[]>(c, "_eventHandlers");

            if (handlers != null) break;

            c = GetPrivateMember<IMessageConsumer>(c, "_inner");
        }

        return handlers;
    }

    public static TMember? GetPrivateMember<TMember>(this object instance, string name) where TMember : class
        => GetMember<TMember>(instance.GetType(), instance, name);

    static TMember? GetMember<TMember>(Type instanceType, object instance, string name)
        where TMember : class {
        const BindingFlags flags = BindingFlags.Instance
                                 | BindingFlags.Public
                                 | BindingFlags.NonPublic
                                 | BindingFlags.Static;

        var field  = instanceType.GetField(name, flags);
        var prop   = instanceType.GetProperty(name, flags);
        var member = prop?.GetValue(instance) ?? field?.GetValue(instance);

        return member == null && instanceType.BaseType != null
            ? GetMember<TMember>(instanceType.BaseType, instance, name) : member as TMember;
    }
}