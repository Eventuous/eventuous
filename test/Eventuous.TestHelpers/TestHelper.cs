using System.Reflection;

namespace Eventuous.TestHelpers;

public static class TestHelper {
    public static TMember? GetPrivateMember<TMember>(this object instance, string name)
        where TMember : class
        => GetMember<TMember>(instance.GetType(), instance, name);

    static TMember? GetMember<TMember>(Type instanceType, object instance, string name)
        where TMember : class
        => GetMember(instanceType, instance, name) as TMember;

    static object? GetMember(Type instanceType, object instance, string name) {
        const BindingFlags flags = BindingFlags.Instance
          | BindingFlags.Public
          | BindingFlags.NonPublic
          | BindingFlags.Static;

        var field  = instanceType.GetField(name, flags);
        var prop   = instanceType.GetProperty(name, flags);
        var member = prop?.GetValue(instance) ?? field?.GetValue(instance);

        return member == null && instanceType.BaseType != null
            ? GetMember(instanceType.BaseType, instance, name)
            : member;
    }
}
