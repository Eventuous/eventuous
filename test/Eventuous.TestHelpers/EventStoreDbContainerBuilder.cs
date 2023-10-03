using System.Runtime.InteropServices;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Testcontainers.EventStoreDb;

namespace Eventuous.TestHelpers;

public sealed class EventStoreDbContainerBuilder : ContainerBuilder<EventStoreDbContainerBuilder, EventStoreDbContainer, EventStoreDbConfiguration> {
    public static readonly string ContainerTag = RuntimeInformation.ProcessArchitecture switch {
        // Architecture.Arm64 => "22.10.2-alpha-arm64v8",
        _                  => "22.10.2-buster-slim"
    };

    public static readonly string EventStoreDbImage = $"eventstore/eventstore:{ContainerTag}";

    public const ushort EventStoreDbPort = 2113;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreDbBuilder" /> class.
    /// </summary>
    public EventStoreDbContainerBuilder()
        : this(new EventStoreDbConfiguration()) {
        DockerResourceConfiguration = Init().DockerResourceConfiguration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreDbBuilder" /> class.
    /// </summary>
    /// <param name="dockerResourceConfiguration">The Docker resource configuration.</param>
    EventStoreDbContainerBuilder(EventStoreDbConfiguration dockerResourceConfiguration)
        : base(dockerResourceConfiguration) {
        DockerResourceConfiguration = dockerResourceConfiguration;
    }

    /// <inheritdoc />
    protected override EventStoreDbConfiguration DockerResourceConfiguration { get; }

    /// <inheritdoc />
    public override EventStoreDbContainer Build() {
        return new EventStoreDbContainer(DockerResourceConfiguration, TestcontainersSettings.Logger);
    }

    /// <inheritdoc />
    protected override EventStoreDbContainerBuilder Init() {
        return base.Init()
            .WithImage(EventStoreDbImage)
            .WithPortBinding(EventStoreDbPort, true)
            .WithEnvironment("EVENTSTORE_CLUSTER_SIZE", "1")
            .WithEnvironment("EVENTSTORE_RUN_PROJECTIONS", "All")
            .WithEnvironment("EVENTSTORE_START_STANDARD_PROJECTIONS", "true")
            .WithEnvironment("EVENTSTORE_INSECURE", "true")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy());
    }

    /// <inheritdoc />
    protected override EventStoreDbContainerBuilder Clone(IContainerConfiguration resourceConfiguration) {
        return Merge(DockerResourceConfiguration, new EventStoreDbConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override EventStoreDbContainerBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration) {
        return Merge(DockerResourceConfiguration, new EventStoreDbConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override EventStoreDbContainerBuilder Merge(EventStoreDbConfiguration oldValue, EventStoreDbConfiguration newValue) {
        return new EventStoreDbContainerBuilder(new EventStoreDbConfiguration(oldValue, newValue));
    }
}
