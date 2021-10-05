using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.AspNetCore.Tests.Sut;

public static class Builder {
    public static IWebHostBuilder BuildHost() => new WebHostBuilder().UseStartup<Startup>();

    public class Startup {
        public void ConfigureServices(IServiceCollection services) {
            services.AddAggregateStore<FakeStore>();
            services.AddSingleton<TestDependency>();

            services.AddAggregateFactory<TestAggregate, TestState, TestId>(
                sp => new TestAggregate(sp.GetRequiredService<TestDependency>())
            );
        }

        public void Configure(IApplicationBuilder app) => app.UseAggregateFactory();
    }
}
