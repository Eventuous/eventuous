using System.Runtime.CompilerServices;
using VerifyTests.DiffPlex;

namespace Eventuous.Tests.AspNetCore.Web.Fixture;

public static class ModuleInitializer {
    [ModuleInitializer]
    public static void Initialize() =>
        VerifyDiffPlex.Initialize(OutputType.Compact);
}
