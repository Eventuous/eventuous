using Eventuous.TestHelpers.TUnit;

namespace Eventuous.Tests.Extensions.AspNetCore.Fixture;

public abstract class TestBaseWithLogs : IDisposable {
    readonly TestEventListener _listener = new();

    public void Dispose() => _listener.Dispose();
}
