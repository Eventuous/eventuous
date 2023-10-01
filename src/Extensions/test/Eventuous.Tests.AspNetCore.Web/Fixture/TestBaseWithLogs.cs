using Eventuous.TestHelpers;

namespace Eventuous.Tests.AspNetCore.Web.Fixture;

[UsesVerify]
public abstract class TestBaseWithLogs(ITestOutputHelper output) : IDisposable {
    readonly TestEventListener _listener = new(output);

    public void Dispose()
        => _listener.Dispose();
}
