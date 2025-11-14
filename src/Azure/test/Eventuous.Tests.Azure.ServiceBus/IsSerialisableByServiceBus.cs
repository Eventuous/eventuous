using static Eventuous.Azure.ServiceBus.Shared.ServiceBusHelper;

namespace Eventuous.Tests.Azure.ServiceBus;

public class IsSerialisableByServiceBus {
    public static IEnumerable<Func<object?>> PassingTestData() {
        yield return () => "string";
        yield return () => 123;
        yield return () => 123L;
        yield return () => (short)1;
        yield return () => (byte)1;
        yield return () => 123u;
        yield return () => 123UL;
        yield return () => 1.23f;
        yield return () => 1.23d;
        yield return () => 12.34m;
        yield return () => true;
        yield return () => 'c';
        yield return () => Guid.NewGuid();
        yield return () => DateTime.UtcNow;
        yield return () => DateTimeOffset.UtcNow;
        yield return () => TimeSpan.FromMinutes(5);
        yield return () => new Uri("https://example.com");
        yield return () => new MemoryStream();
    }

    public static IEnumerable<Func<object?>> FailingTestData() {
        yield return () => null;
        yield return () => new();                                              // plain object
        yield return () => new Dictionary<object, string> { [new()] = "v" };   // dictionary with non-string key
        yield return () => new Dictionary<string, object> { ["k"]   = new() }; // dictionary with non-serializable value
        yield return () => new List<object> { new() };                         // list with non-serializable item
        yield return () => Task.CompletedTask;                                 // Task
        yield return () => new Action(() => { });                              // delegate
        yield return () => new WeakReference(new());                           // complex type
    }

    [Test]
    [MethodDataSource(nameof(PassingTestData))]
    public async Task Passes(object value) => await Assert.That(IsSerialisableByServiceBus(value)).IsTrue();

    [Test]
    [MethodDataSource(nameof(FailingTestData))]
    public async Task Fails(object value) => await Assert.That(IsSerialisableByServiceBus(value)).IsFalse();
}
