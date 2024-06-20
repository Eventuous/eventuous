using Eventuous.Tests.AspNetCore.Sut;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.AspNetCore;

public class AggregateFactoryRegistrationTests {
    readonly AggregateFactoryRegistry _registry;

    public AggregateFactoryRegistrationTests() {
        var host = BuildHost();
        var app  = host.Build();
        app.UseAggregateFactory();
        _registry = app.Services.GetRequiredService<AggregateFactoryRegistry>();
    }

    [Fact]
    public void ShouldCreateNewAggregateWithExplicitFunction() {
        var instance = _registry.CreateInstance<TestAggregate, TestState>();
        instance.Should().BeOfType<TestAggregate>();
        instance.Dependency.Should().NotBeNull();
        instance.State.Should().NotBeNull();
    }

    [Fact]
    public void ShouldCreateNewAggregateByResolve() {
        var instance = _registry.CreateInstance<AnotherTestAggregate, TestState>();
        instance.Should().BeOfType<AnotherTestAggregate>();
        instance.Dependency.Should().NotBeNull();
        instance.State.Should().NotBeNull();
    }

    [Fact]
    public void ShouldCreateTwoSeparateInstances() {
        var instance1 = _registry.CreateInstance<AnotherTestAggregate, TestState>();
        var instance2 = _registry.CreateInstance<AnotherTestAggregate, TestState>();
        instance1.Should().NotBeSameAs(instance2);
    }

    static WebApplicationBuilder BuildHost() {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAggregateStore<FakeStore>();
        builder.Services.AddSingleton<TestDependency>();
        builder.Services.AddAggregate<TestAggregate, TestState>(sp => new TestAggregate(sp.GetRequiredService<TestDependency>()));
        builder.Services.AddAggregate<AnotherTestAggregate, TestState>();

        return builder;
    }
}
