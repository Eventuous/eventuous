namespace Eventuous;

public static class TypeExtensions {
    public static T? GetAttribute<T>(this Type type) where T : class
        => Attribute.GetCustomAttribute(type, typeof(T)) as T;
}