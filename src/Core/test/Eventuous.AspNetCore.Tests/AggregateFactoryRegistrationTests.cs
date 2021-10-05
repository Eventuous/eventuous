using Eventuous.AspNetCore.Tests.Sut;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Eventuous.AspNetCore.Tests;

public class AggregateFactoryRegistrationTests {
    readonly TestServer _sut;

    public AggregateFactoryRegistrationTests() {
        _sut = new TestServer(Builder.BuildHost());
    }

    [Fact]
    public void ShouldCreateNewAggregate() {
        var instance = AggregateFactoryRegistry.Instance.CreateInstance<TestAggregate>();
        instance.Should().BeOfType<TestAggregate>();
        instance.Dependency.Should().NotBeNull();
    }
}
