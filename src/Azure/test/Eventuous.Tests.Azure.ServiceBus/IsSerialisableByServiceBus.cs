using static Eventuous.Azure.ServiceBus.Shared.ServiceBusHelper;
using System.Threading.Tasks;

namespace Eventuous.Tests.Azure.ServiceBus;

public class IsSerialisableByServiceBus {
    public static IEnumerable<Func<object?>> PassingTestData() {
        yield return () => "string";
        yield return () => 123; // int
        yield return () => 123L; // long
        yield return () => (short)1;
        yield return () => (byte)1;
        yield return () => 123u; // uint
        yield return () => 123UL; // ulong
        yield return () => 1.23f; // float
        yield return () => 1.23d; // double
        yield return () => 12.34m; // decimal
        yield return () => true; // bool
        yield return () => 'c'; // char
        yield return () => Guid.NewGuid();
        yield return () => DateTime.UtcNow;
        yield return () => DateTimeOffset.UtcNow;
        yield return () => TimeSpan.FromMinutes(5);
        yield return () => new Uri("https://example.com");
        yield return () => new MemoryStream(); // Stream
    }

    public static IEnumerable<Func<object?>> FailingTestData() {
        yield return () => null;
        yield return () => new object(); // plain object
        yield return () => new Dictionary<object, string> { [new object()] = "v" }; // dictionary with non-string key
        yield return () => new Dictionary<string, object> { ["k"] = new object() }; // dictionary with non-serializable value
        yield return () => new List<object> { new() }; // list with non-serializable item
        yield return () => Task.CompletedTask; // Task
        yield return () => new Action(() => { }); // delegate
        yield return () => new WeakReference(new object()); // complex type
    }

    [Test]
    [MethodDataSource(nameof(PassingTestData))]
    public async Task Passes(object value) => await Assert.That(IsSerialisableByServiceBus(value)).IsTrue();

    [Test]
    [MethodDataSource(nameof(FailingTestData))]
    public async Task Fails(object value) => await Assert.That(IsSerialisableByServiceBus(value)).IsFalse();
}
