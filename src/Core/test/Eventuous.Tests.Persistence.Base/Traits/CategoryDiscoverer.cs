// using JetBrains.Annotations;
//
// // ReSharper disable once CheckNamespace
// namespace Xunit;
//
// using Sdk;
//
// /// <summary>
// /// This class discovers all the tests and test classes that have
// /// applied the Category attribute
// /// </summary>
// [UsedImplicitly]
// public class CategoryDiscoverer : ITraitDiscoverer {
//     /// <summary>
//     /// Gets the trait values from the Category attribute.
//     /// </summary>
//     /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
//     /// <returns>The trait values.</returns>
//     public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute) {
//         var categoryName = traitAttribute.GetNamedArgument<string>("Name");
//
//         yield return new("Category", categoryName);
//     }
// }
