using Eventuous.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Connectors.Base;

public static class Hosting {
    public static WebApplication GetHost(this WebApplicationBuilder builder) {
        var host = builder.Build();
        host.AddEventuousLogs();
        return host;
    }

    public static async Task RunConnector(this WebApplication host) {
        var jobs = host.Services.GetServices<IStartupJob>().ToArray();

        if (jobs.Length > 0) {
            await Task.WhenAll(jobs.Select(x => x.Run()));
        }

        await host.RunAsync();
    }
}
