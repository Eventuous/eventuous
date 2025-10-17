using Eventuous.Extensions.AspNetCore.Generators;

namespace Eventuous.Tests.Extensions.AspNetCore.Analyzers;

public partial class HttpCommandAnnotationTests {
    [Test]
    public async Task Should_warn_EVTA002_for_route_mismatch() {
        const string source
            = """
              using Eventuous;
              using Eventuous.Extensions.AspNetCore.Http;
              using Microsoft.AspNetCore.Routing;

              public class BookingState : State<BookingState> {}

              [HttpCommand<BookingState>(Route = "bar")]
              public class ImportBookingHttp { }

              public class ImportBooking { }

              public class TestHost {
                  public void Map(IEndpointRouteBuilder app) {
                      app.MapCommands<BookingState>()
                         .MapCommand<ImportBookingHttp, ImportBooking>("foo", (c, ctx) => new ImportBooking());
                  }
              }
              """;

        var compilation = CreateCompilation(source);
        var analyzer    = new HttpCommandStateMismatchAnalyzer();

        var diagnostics = await GetAnalyzerDiagnosticsAsync(compilation, analyzer);

        var evta002 = diagnostics.Where(d => d.Id == HttpCommandStateMismatchAnalyzer.RouteDiagnosticId).ToArray();
        await Assert.That(evta002.Length).IsEqualTo(1);
        await Assert.That(evta002[0].GetMessage()).Contains("ImportBookingHttp");
    }
}
