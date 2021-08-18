using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Tests.EventStore.Fixtures;
using Eventuous.Tests.SutApp;
using Xunit;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.EventStore {
    public class StreamSubscriptionTests {
        [Fact]
        public async Task StreamSubscriptionGetsDeletedEvents() {
            var service  = new BookingService(Instance.AggregateStore);
            var commands = Enumerable.Range(0, 100)
                .Select(_ => DomainFixture.CreateImportBooking());

            await Task.WhenAll(commands.Select(x => service.Handle(x, CancellationToken.None)));
        }
    }
}