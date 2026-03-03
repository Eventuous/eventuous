using Eventuous.Tests.Sqlite;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Limiter>]

namespace Eventuous.Tests.Sqlite;

public class Limiter : IParallelLimit {
    public int Limit => 2;
}
