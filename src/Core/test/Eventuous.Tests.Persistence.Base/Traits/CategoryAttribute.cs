using JetBrains.Annotations;
using Xunit.v3;

// ReSharper disable once CheckNamespace

namespace Xunit;

/// <summary>
/// Apply this attribute to your test method to specify a category.
/// </summary>
[UsedImplicitly]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CategoryAttribute(string category) : Attribute, ITraitAttribute {
    // [UsedImplicitly]
    // public string Name { get; } = category;
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new("Category", category)];
}
