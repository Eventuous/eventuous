using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TUnit.Assertions.AssertConditions.Interfaces;
using TUnit.Assertions.AssertionBuilders;
using TUnit.Assertions.Enums;

namespace Eventuous.TestHelpers.TUnit;

[SuppressMessage("Usage", "TUnitAssertions0003:Compiler argument populated")]
public static class Assertions {
    public static InvokableValueAssertionBuilder<TActual> CollectionEquivalentTo
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActual, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInner>(
            this IValueSource<TActual>                          valueSource,
            IEnumerable<TInner>                                 expected,
            [CallerArgumentExpression(nameof(expected))] string doNotPopulateThisValue = ""
        )
        where TActual : IEnumerable<TInner> {
        return CollectionsIsExtensions.IsEquivalentTo(valueSource, expected, doNotPopulateThisValue);
    }

    public static InvokableValueAssertionBuilder<TActual> CollectionIsEquivalentTo
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActual, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInner>(
            this IValueSource<TActual>                          valueSource,
            IEnumerable<TInner>                                 expected,
            IEqualityComparer<TInner>                           comparer,
            [CallerArgumentExpression(nameof(expected))] string doNotPopulateThisValue = ""
        )
        where TActual : IEnumerable<TInner> {
        return CollectionsIsExtensions.IsEquivalentTo(valueSource, expected, comparer, doNotPopulateThisValue);
    }

    public static InvokableValueAssertionBuilder<TActual> CollectionEquivalentTo
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActual, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInner>(
            this IValueSource<TActual>                          valueSource,
            IEnumerable<TInner>                                 expected,
            CollectionOrdering                                  collectionOrdering,
            [CallerArgumentExpression(nameof(expected))] string doNotPopulateThisValue = ""
        )
        where TActual : IEnumerable<TInner> {
        return CollectionsIsExtensions.IsEquivalentTo(valueSource, expected, collectionOrdering, doNotPopulateThisValue);
    }

    public static InvokableValueAssertionBuilder<TActual> CollectionEquivalentTo
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActual, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInner>(
            this IValueSource<TActual>                          valueSource,
            IEnumerable<TInner>                                 expected,
            IEqualityComparer<TInner>                           comparer,
            CollectionOrdering                                  collectionOrdering,
            [CallerArgumentExpression(nameof(expected))] string doNotPopulateThisValue = ""
        )
        where TActual : IEnumerable<TInner> {
        return CollectionsIsExtensions.IsEquivalentTo(valueSource, expected, comparer, collectionOrdering, doNotPopulateThisValue);
    }

}
