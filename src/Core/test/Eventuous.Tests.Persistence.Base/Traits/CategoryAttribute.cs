// ReSharper disable once CheckNamespace
namespace Xunit;

using Sdk;

/// <summary>
/// Apply this attribute to your test method to specify a category.
/// </summary>
[TraitDiscoverer("CategoryDiscoverer", "TraitExtensibility")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CategoryAttribute(string category) : Attribute, ITraitAttribute {
    public string Name { get; } = category;
}
