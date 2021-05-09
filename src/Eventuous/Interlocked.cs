using System.Threading;

namespace Eventuous {
    public class InterlockedSemaphore {
        int _value;

        public InterlockedSemaphore(bool initialValue = false) => _value = initialValue ? 1 : 0;
        
        public bool CanMove() => CompareExchange(true, false);

        public bool Close() => Set(true);
        
        public bool Open() => Set(false);

        public bool IsClosed() => _value == 1;

        bool Set(bool newValue) {
            var oldValue = Interlocked.Exchange(ref _value, newValue ? 1 : 0);

            return oldValue == 1;
        }

        bool CompareExchange(bool newValue, bool comparand) {
            var oldValue = Interlocked.CompareExchange(ref _value, newValue ? 1 : 0, comparand ? 1 : 0);
        
            return oldValue == 1;
        }
    }
}