using JetBrains.Annotations;

// ReSharper disable once CheckNamespace

namespace Xunit;

using Sdk;

/// <summary>
/// Apply this attribute to your test method to specify a category.
/// </summary>
[UsedImplicitly]
[TraitDiscoverer("CategoryDiscoverer", "TraitExtensibility")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CategoryAttribute(string category) : Attribute, ITraitAttribute {
    [UsedImplicitly]
    public string Name { get; } = category;
}
