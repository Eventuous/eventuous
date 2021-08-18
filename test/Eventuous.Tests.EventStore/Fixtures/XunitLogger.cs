using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Eventuous.Tests.EventStore.Fixtures {
    public class XunitLogger<T> : ILogger<T>, IDisposable {
        readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
            => _output = output;

        public void Log<TState>(
            LogLevel                        logLevel,
            EventId                         eventId,
            TState                          state,
            Exception                       exception,
            Func<TState, Exception, string> formatter
        )
            => _output.WriteLine(state?.ToString());

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) => this;

        public void Dispose() { }
    }
}
