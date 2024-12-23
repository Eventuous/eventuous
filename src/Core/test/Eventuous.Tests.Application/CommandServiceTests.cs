using Eventuous.Sut.App;
using Eventuous.Sut.Domain;

namespace Eventuous.Tests.Application;

// ReSharper disable once UnusedType.Global
[InheritsTests]
public class CommandServiceTests : ServiceTestBase {
    protected override ICommandService<BookingState> CreateService(AmendEvent<ImportBooking>? amendEvent = null, AmendEvent? amendAll = null)
        => new ExtendedService(Store, TypeMap, amendEvent, amendAll);

    class ExtendedService : BookingService {
        public ExtendedService(
                IEventStore                store,
                ITypeMapper                typeMap,
                AmendEvent<ImportBooking>? amendEvent = null,
                AmendEvent?                amendAll   = null
            ) : base(store, typeMapper: typeMap, amendEvent: amendAll) {
            On<ImportBooking>()
                .InState(ExpectedState.Any)
                .GetId(cmd => new(cmd.BookingId))
                .AmendEvent(amendEvent ?? ((@event, _) => @event))
                .Act((booking, cmd) => booking.Import(cmd.RoomId, new(cmd.CheckIn, cmd.CheckOut), new(cmd.Price)));
        }
    }
}
