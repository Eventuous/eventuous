using Eventuous.Tests.DependencyInjection.Sut;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eventuous.Tests.DependencyInjection;

public class AggregateFactoryRegistrationTests {
    readonly AggregateFactoryRegistry _registry;

    public AggregateFactoryRegistrationTests() {
        var host = BuildHost();
        host.Services.AddAggregate<TestAggregate, TestState>();
        var app  = host.Build();
        _registry = app.Services.GetRequiredService<AggregateFactoryRegistry>();
    }

    [Test]
    public void ShouldCreateNewAggregateWithExplicitFunction() {
        var instance = _registry.CreateInstance<TestAggregate, TestState>();
        instance.ShouldBeOfType<TestAggregate>();
        instance.Dependency.ShouldNotBeNull();
        instance.State.ShouldNotBeNull();
    }

    [Test]
    public void ShouldCreateNewAggregateByResolve() {
        var instance = _registry.CreateInstance<AnotherTestAggregate, TestState>();
        instance.ShouldBeOfType<AnotherTestAggregate>();
        instance.Dependency.ShouldNotBeNull();
        instance.State.ShouldNotBeNull();
    }

    [Test]
    public void ShouldCreateTwoSeparateInstances() {
        var instance1 = _registry.CreateInstance<AnotherTestAggregate, TestState>();
        var instance2 = _registry.CreateInstance<AnotherTestAggregate, TestState>();
        instance1.ShouldNotBeSameAs(instance2);
    }

    static WebApplicationBuilder BuildHost() {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddEventStore<FakeStore>();
        builder.Services.AddSingleton<TestDependency>();
        builder.Services.AddAggregate<TestAggregate, TestState>(sp => new(sp.GetRequiredService<TestDependency>()));
        builder.Services.AddAggregate<AnotherTestAggregate, TestState>();

        return builder;
    }
}
