using System;

namespace Eventuous.Subscriptions {
    public class Disposable : IDisposable {
        readonly Action _dispose;

        public Disposable(Action dispose) => _dispose = dispose;

        public void Dispose() => _dispose();
    }
}