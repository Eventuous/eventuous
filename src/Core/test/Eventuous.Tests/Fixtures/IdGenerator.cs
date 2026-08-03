namespace Eventuous.Tests.Fixtures;

public static class IdGenerator {
    public static string GetId() => Guid.NewGuid().ToString("N");
}
