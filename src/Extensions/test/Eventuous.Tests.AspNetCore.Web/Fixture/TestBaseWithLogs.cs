using Eventuous.TestHelpers;

namespace Eventuous.Tests.AspNetCore.Web.Fixture;

[UsesVerify]
public abstract class TestBaseWithLogs(ITestOutputHelper output) : IDisposable {
    readonly TestEventListener _listener = new(output);

    protected ITestOutputHelper Output { get; } = output;

    public void Dispose()
        => _listener.Dispose();
}
