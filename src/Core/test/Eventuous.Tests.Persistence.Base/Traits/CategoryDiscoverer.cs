// ReSharper disable once CheckNamespace
namespace Xunit;

using Sdk;

/// <summary>
/// This class discovers all of the tests and test classes that have
/// applied the Category attribute
/// </summary>
public class CategoryDiscoverer : ITraitDiscoverer {
    /// <summary>
    /// Gets the trait values from the Category attribute.
    /// </summary>
    /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
    /// <returns>The trait values.</returns>
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute) {
        var categoryName = traitAttribute.GetNamedArgument<string>("Name");

        yield return new KeyValuePair<string, string>("Category", categoryName);
    }
}
