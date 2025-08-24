namespace Eventuous.Tests.Azure.ServiceBus;

[EventType("V1.SomeEvent")]
public record SomeEvent {
    static SomeEvent() => TypeMap.RegisterKnownEventTypes(typeof(SomeEvent).Assembly);

    public string  Id      { get; set; } = Guid.NewGuid().ToString();
    public string  Name    { get; set; } = "Some Event";
    public byte[]? BigData { get; set; } = new byte[1000];

    public static SomeEvent Create() => new() { Id = "test-event", Name = "Hello, World!" };

    public static SomeEvent Create(int i) => new() { Id = $"test-event-{i:0000}", Name = $"Hello, World! {i}" };
}
