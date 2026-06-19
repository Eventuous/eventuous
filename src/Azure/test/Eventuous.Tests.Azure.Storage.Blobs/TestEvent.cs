namespace Eventuous.Tests.Azure.Storage.Blobs;

[EventType("V1.TestEvent")]
public record TestEvent {
    static TestEvent() => TypeMap.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Test Event";
    public int Value { get; set; } = 42;

    public static TestEvent Create() => new() { Id = "test-event", Name = "Test", Value = 1 };

    public static TestEvent Create(int value) => new() { Id = "test-event", Name = "Test", Value = value };
}
