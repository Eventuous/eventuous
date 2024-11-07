namespace Eventuous.Tests.Fixtures;

public class IdGenerator {
    public static string GetId() => Guid.NewGuid().ToString("N");
}
