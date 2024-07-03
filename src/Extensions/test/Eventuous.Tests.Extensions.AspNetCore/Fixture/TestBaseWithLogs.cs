using Eventuous.TestHelpers;

namespace Eventuous.Tests.Extensions.AspNetCore.Fixture;

public abstract class TestBaseWithLogs(ITestOutputHelper output) : IDisposable {
    readonly TestEventListener _listener = new(output);

    public void Dispose() => _listener.Dispose();
}
