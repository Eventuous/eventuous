namespace Eventuous.Tests.OpenTelemetry.Fakes;

public class MessageCounter {
    public int Count;
    public void Increment() => Interlocked.Increment(ref Count);
}
