using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Producers {
    [PublicAPI]
    public class ProducersHostedService : IHostedService {
        readonly IEnumerable<IEventProducer> _producers;

        public ProducersHostedService(IEnumerable<IEventProducer> producers) => _producers = producers;

        public Task StartAsync(CancellationToken cancellationToken)
            => Task.WhenAll(_producers.Select(x => x.Initialize(cancellationToken)));

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.WhenAll(_producers.Select(x => x.Shutdown(cancellationToken)));
    }
}