using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Eventuous;

namespace Bookings.Domain; 

static class DomainModule {
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", MessageId = "The \'ModuleInitializer\' attribute should not be used in libraries")]
    internal static void InitializeDomainModule() => TypeMap.RegisterKnownEventTypes();
}