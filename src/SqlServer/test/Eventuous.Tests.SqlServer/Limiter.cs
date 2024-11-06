using Eventuous.Tests.SqlServer;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Limiter>]

namespace Eventuous.Tests.SqlServer;

public class Limiter : IParallelLimit {
    public int Limit => 2;
}
