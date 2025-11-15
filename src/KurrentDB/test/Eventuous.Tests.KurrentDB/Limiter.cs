using Eventuous.Tests.KurrentDB;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Limiter>]

namespace Eventuous.Tests.KurrentDB;

public class Limiter : IParallelLimit {
    public int Limit => 4;
}
