// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Extensions.AspNetCore.Generators;
using Diags = Eventuous.Extensions.AspNetCore.Generators.Diagnostics;

namespace Eventuous.Tests.Extensions.AspNetCore.Analyzers;

public partial class HttpCommandAnnotationTests {
    [Test]
    public async Task Should_warn_EVTA001_for_state_mismatch() {
        const string source
            = """
              using Eventuous;
              using Eventuous.Extensions.AspNetCore.Http;
              using Microsoft.AspNetCore.Routing;

              public class BookingState : State<BookingState> {}
              public class BrookingState : State<BrookingState> {}

              [HttpCommand<BrookingState>]
              public class ImportBookingHttp3 {}

              public class ImportBooking {}

              public class TestHost {
                  public void Map(IEndpointRouteBuilder app) {
                      app.MapCommands<BookingState>()
                         .MapCommand<ImportBookingHttp3, ImportBooking>((c, ctx) => new ImportBooking());
                  }
              }
              """;

        var compilation = CreateCompilation(source);
        var analyzer    = new HttpCommandStateMismatchAnalyzer();

        var diagnostics = await GetAnalyzerDiagnosticsAsync(compilation, analyzer);

        var evta001 = diagnostics.Where(d => d.Id == Diags.DiagnosticId).ToArray();
        await Assert.That(evta001.Length).IsEqualTo(1);
        await Assert.That(evta001[0].GetMessage()).Contains("ImportBookingHttp3");
    }
}
