using Eventuous.Tests.Postgres;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Limiter>]

namespace Eventuous.Tests.Postgres;

public class Limiter : IParallelLimit {
    public int Limit => 4;
}
