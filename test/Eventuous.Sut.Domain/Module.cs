using System.Runtime.CompilerServices;

namespace Eventuous.Sut.Domain {
    public static class Module {
        [ModuleInitializer]
        public static void Initialize() => TypeMap.RegisterKnownEventTypes(); 
    }
}