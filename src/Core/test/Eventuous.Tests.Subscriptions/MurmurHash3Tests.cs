using Eventuous.Subscriptions.Filters.Partitioning;

namespace Eventuous.Tests.Subscriptions;

public class MurmurHash3Tests {
    // Expected values were captured from the original pointer-based implementation, so this test
    // proves the span-based rewrite produces identical hashes (and therefore identical partitions).
    [Test]
    [Arguments("", 2183108998u)]
    [Arguments("a", 3484832574u)]
    [Arguments("ab", 2947271106u)]
    [Arguments("abc", 3529399110u)]
    [Arguments("Order-123", 1952828587u)]
    [Arguments("ünïcødé-ключ-😀", 2930121001u)]
    public async Task ShouldProduceKnownHash(string key, uint expected) {
        await Assert.That(MurmurHash3.Hash(key)).IsEqualTo(expected);
    }

    [Test]
    public async Task ShouldThrowOnNull() {
        await Assert.That(() => MurmurHash3.Hash(null!)).Throws<ArgumentNullException>();
    }
}
