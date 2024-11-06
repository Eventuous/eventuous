using Eventuous.Tests.EventStore;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Limiter>]

namespace Eventuous.Tests.EventStore;

public class Limiter : IParallelLimit {
    public int Limit => 4;
}
