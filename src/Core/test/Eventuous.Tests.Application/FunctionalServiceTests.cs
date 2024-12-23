namespace Eventuous.Tests.Application;

using Sut.Domain;

// ReSharper disable once UnusedType.Global
[InheritsTests]
public class FunctionalServiceTests : ServiceTestBase {
    protected override ICommandService<BookingState> CreateService(AmendEvent<ImportBooking>? amendEvent = null, AmendEvent? amendAll = null)
        => new ExtendedService(Store, TypeMap, amendEvent, amendAll);

    class ExtendedService : BookingFuncService {
        public ExtendedService(
                IEventStore                store,
                ITypeMapper?               typeMap,
                AmendEvent<ImportBooking>? amendEvent = null,
                AmendEvent?                amendAll   = null
            ) : base(store, typeMap, amendAll) {
            On<ImportBooking>()
                .InState(ExpectedState.Any)
                .GetStream(cmd => GetStream(cmd.BookingId))
                .AmendEvent(amendEvent ?? ((@event, _) => @event))
                .Act(ImportBooking);

            return;

            static IEnumerable<object> ImportBooking(BookingState state, object[] events, ImportBooking cmd)
                => [new BookingEvents.BookingImported(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut)];
        }
    }
}
